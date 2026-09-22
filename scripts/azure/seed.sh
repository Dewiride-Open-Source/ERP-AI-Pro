#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/seed.sh [--dry-run] [--label <local-dev|production|all>] [--no-sentinel] [--params <file>]

Imports infra/appconfig into the App Configuration store: the unlabelled
defaults, each label file, the labelled feature flags and the Key Vault
references (only under the labels a reference lists, and only when the secret
already exists in that label'"'"'s vault), then bumps the labelled Erp:Sentinel so
a running API picks the change up at its next check. Never deletes a key; safe
to run repeatedly.

Options:
  --dry-run          Preview every import and print every other write; write nothing.
  --label <label>    Seed only local-dev or production (default: all).
  --no-sentinel      Do not bump Erp:Sentinel after seeding.
  --params <file>    Parameter file (default: scripts/azure/params.env).
  --help             Show this help.'

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/appconfig.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/appconfig.sh"

readonly SENTINEL_KEY='Erp:Sentinel'
readonly ALL_LABELS=(local-dev production)
readonly SEED_DIRECTORY='infra/appconfig'
readonly SEED_FILES_CLI='scripts/azure/lib/seed-files.ts'
readonly SECRET_HINT='create it first (the sign-in certificates come from: bash scripts/azure/entra.sh)'

LABEL_SELECTION='all'
BUMP_SENTINEL=1

parse_seed_args() {
  local -a rest=()
  while (( $# > 0 )); do
    case "$1" in
      --label)
        [[ -n "${2:-}" ]] || die "--label requires local-dev, production or all"
        LABEL_SELECTION="$2"
        shift
        ;;
      --label=*)
        LABEL_SELECTION="${1#--label=}"
        ;;
      --no-sentinel)
        BUMP_SENTINEL=0
        ;;
      *)
        rest+=("$1")
        ;;
    esac
    shift
  done
  parse_args "${rest[@]}"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  case "$LABEL_SELECTION" in
    all | local-dev | production) ;;
    *) die "--label must be local-dev, production or all; '$LABEL_SELECTION' was rejected" ;;
  esac
}

selected_labels() {
  if [[ "$LABEL_SELECTION" == all ]]; then
    printf '%s\n' "${ALL_LABELS[@]}"
  else
    printf '%s\n' "$LABEL_SELECTION"
  fi
}

seed_rows() {
  local command="$1" file="$2"
  node "$SEED_FILES_CLI" "$command" "$file" | tr -d '\r'
}

vault_for_label() {
  case "$1" in
    local-dev) printf '%s' "$ERP_AZURE_KEYVAULT_DEV_NAME" ;;
    production) printf '%s' "$ERP_AZURE_KEYVAULT_PROD_NAME" ;;
    *) die "vault_for_label: unknown label '$1'" ;;
  esac
}

vault_uri() {
  local vault="$1"
  local uri
  uri="$(az_read keyvault show --name "$vault" --query properties.vaultUri --output tsv)" || die "vault '$vault' could not be read; run: bash scripts/azure/provision.sh"
  [[ -n "$uri" ]] || die "vault '$vault' reported no URI"
  printf '%s' "${uri%/}"
}

require_secret() {
  local vault="$1" secret="$2"
  local count
  count="$(az_read keyvault secret list --vault-name "$vault" --include-managed true --query "length([?name=='$secret'])" --output tsv)" \
    || die "secrets of vault '$vault' could not be listed; a 403 means a role assignment is still propagating"
  [[ "$count" == 1 ]] || die "secret '$secret' does not exist in vault '$vault'; $SECRET_HINT"
}

import_settings() {
  local file="$1" label="$2"
  local count
  count="$(seed_rows settings "$file" | grep -c .)" || true
  if (( count == 0 )); then
    log_info "$file: no keys, import skipped"
    return 0
  fi
  local -a label_args=()
  [[ -z "$label" ]] || label_args=(--label "$label")
  local -a command=(az appconfig kv import --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --source file --format json
    --separator : --path "$file" "${label_args[@]}" --skip-features --import-mode ignore-match)
  log_info "$file: $count key(s) into label $(appconfig_label_display "$label")"
  if (( DRY_RUN )); then
    "${command[@]}" --dry-run
    return 0
  fi
  retry -- "${command[@]}" --yes
}

feature_state() {
  local id="$1" label="$2"
  az_read appconfig feature list --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --feature "$id" --label "$label" \
    --query '[0].state' --output tsv
}

ensure_feature_once() {
  local id="$1" label="$2" enabled="$3"
  local desired='off'
  [[ "$enabled" != true ]] || desired='on'
  local state
  appconfig_read_current state feature_state "$id" "$label" || return 1
  if [[ -z "$state" ]]; then
    run az appconfig feature set --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --feature "$id" --label "$label" \
      --yes --output none --only-show-errors || return 1
    appconfig_log_write ".appconfig.featureflag/$id" "$label" "" " (feature flag)"
    state='off'
  fi
  if [[ "$state" == "$desired" ]]; then
    log_info "$id [$label] unchanged ($state)"
    return 0
  fi
  local verb='disable'
  [[ "$desired" != on ]] || verb='enable'
  run az appconfig feature "$verb" --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --feature "$id" --label "$label" \
    --yes --output none --only-show-errors || return 1
  appconfig_log_write ".appconfig.featureflag/$id" "$label" "$state" " (now $desired)"
}

ensure_feature() {
  retry -- ensure_feature_once "$1" "$2" "$3"
}

seed_feature_flags() {
  local label="$1"
  local -a rows=()
  mapfile -t rows < <(seed_rows flags "$SEED_DIRECTORY/feature-flags.json")
  local row id flag_label enabled
  for row in "${rows[@]}"; do
    IFS=$'\t' read -r id flag_label enabled <<< "$row"
    [[ "$flag_label" == "$label" ]] || continue
    ensure_feature "$id" "$label" "$enabled"
  done
}

seed_key_vault_references() {
  local label="$1"
  local vault base_uri key secret ref_labels
  vault="$(vault_for_label "$label")"
  local -a rows=()
  mapfile -t rows < <(seed_rows references "$SEED_DIRECTORY/key-vault-references.json")
  (( ${#rows[@]} > 0 )) || return 0
  base_uri="$(vault_uri "$vault")"
  local row
  for row in "${rows[@]}"; do
    IFS=$'\t' read -r key secret ref_labels <<< "$row"
    if [[ ",$ref_labels," != *",$label,"* ]]; then
      log_info "$key [$label] not seeded under this label"
      continue
    fi
    require_secret "$vault" "$secret"
    ensure_kv_reference "$key" "$label" "$base_uri/secrets/$secret"
  done
}

read_back() {
  local label="$1"
  local label_filter="$label"
  [[ -n "$label_filter" ]] || label_filter="$APPCONFIG_NULL_LABEL_FILTER"
  log_step "Read-back: keys with label $(appconfig_label_display "$label")"
  az_read appconfig kv list --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --label "$label_filter" --key 'Erp:*' \
    --query "[].{key:key, value:value, label:label}" --output table
  if [[ -n "$label" ]]; then
    log_step "Read-back: feature flags with label $label"
    az_read appconfig feature list --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --label "$label" \
      --query "[].{name:name, state:state, label:label}" --output table
  fi
}

main() {
  parse_seed_args "$@"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az node grep

  log_step "Validating $SEED_DIRECTORY"
  node "$SEED_FILES_CLI" validate

  log_step "Checking the Azure CLI sign-in"
  ensure_login
  (( DRY_RUN )) && log_info "dry run: imports are previewed with --dry-run and every other write is printed"

  log_step "Unlabelled defaults"
  import_settings "$SEED_DIRECTORY/defaults.json" ''

  local -a labels=()
  mapfile -t labels < <(selected_labels)
  local label
  for label in "${labels[@]}"; do
    log_step "Label $label: settings"
    import_settings "$SEED_DIRECTORY/$label.json" "$label"
    log_step "Label $label: feature flags"
    seed_feature_flags "$label"
    log_step "Label $label: Key Vault references"
    seed_key_vault_references "$label"
    if (( BUMP_SENTINEL )); then
      log_step "Label $label: sentinel"
      ensure_kv "$SENTINEL_KEY" "$label" "$(utc_timestamp)"
    fi
  done

  read_back ''
  for label in "${labels[@]}"; do
    read_back "$label"
  done

  log_step "Summary"
  if (( DRY_RUN )); then
    log_info "nothing was written"
  elif (( BUMP_SENTINEL )); then
    log_info "a running API reloads within its refresh interval; restart the api container to apply the change at once"
  else
    log_info "the sentinel was not bumped: a running API keeps its current values until the next bump or restart"
  fi
  log_info "next step: bash scripts/azure/verify.sh --labels"
}

main "$@"
