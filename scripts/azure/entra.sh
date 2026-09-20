#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/entra.sh [--dry-run] [--params <file>]
       bash scripts/azure/entra.sh --rotate-signin-certificate <local-dev|production> [--dry-run] [--params <file>]
       bash scripts/azure/entra.sh --rotate-runtime-certificate [--dry-run] [--params <file>]
       bash scripts/azure/entra.sh --prune-old-credentials <local-dev|production|runtime> [--dry-run] [--params <file>]

Creates or converges the three single-tenant app registrations (sign-in for
local-dev, sign-in for production, runtime for the server), their service
principals, app roles, Microsoft Graph consent, Key Vault certificates, key
credentials, the Erp.Admin assignment of the operator and the App Configuration
identity ids. Safe to run repeatedly; never deletes a registration.

Options:
  --dry-run                              Print every write that would run; write nothing.
  --params <file>                        Parameter file (default: scripts/azure/params.env).
  --rotate-signin-certificate <label>    Create a new version of the sign-in certificate of local-dev or production and register it.
  --rotate-runtime-certificate           Create a new version of the runtime certificate and register it.
  --prune-old-credentials <target>       Delete every key credential of local-dev, production or runtime that is not the current certificate.
  --help                                 Show this help.'

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/appconfig.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/appconfig.sh"
# shellcheck source=lib/graph.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/graph.sh"

readonly SENTINEL_KEY='Erp:Sentinel'
readonly RUNTIME_TARGET='runtime'

MODE=converge
TARGET=''

set_mode() {
  [[ "$MODE" == converge ]] || die "only one of --rotate-signin-certificate, --rotate-runtime-certificate and --prune-old-credentials can be given"
  MODE="$1"
}

require_target() {
  local option="$1" value="$2" allow_runtime="$3"
  [[ -n "$value" ]] || die "$option requires a value"
  if [[ "$value" == "$RUNTIME_TARGET" ]]; then
    (( allow_runtime )) || die "$option accepts local-dev or production; got '$value'"
    printf '%s' "$value"
    return 0
  fi
  local label
  for label in "${SIGNIN_LABELS[@]}"; do
    if [[ "$value" == "$label" ]]; then
      printf '%s' "$value"
      return 0
    fi
  done
  if (( allow_runtime )); then
    die "$option accepts local-dev, production or runtime; got '$value'"
  fi
  die "$option accepts local-dev or production; got '$value'"
}

parse_entra_args() {
  local -a rest=()
  while (( $# > 0 )); do
    case "$1" in
      --rotate-signin-certificate)
        set_mode rotate-signin
        TARGET="$(require_target "$1" "${2:-}" 0)"
        shift
        ;;
      --rotate-signin-certificate=*)
        set_mode rotate-signin
        TARGET="$(require_target "--rotate-signin-certificate" "${1#--rotate-signin-certificate=}" 0)"
        ;;
      --rotate-runtime-certificate)
        set_mode rotate-runtime
        TARGET="$RUNTIME_TARGET"
        ;;
      --prune-old-credentials)
        set_mode prune
        TARGET="$(require_target "$1" "${2:-}" 1)"
        shift
        ;;
      --prune-old-credentials=*)
        set_mode prune
        TARGET="$(require_target "--prune-old-credentials" "${1#--prune-old-credentials=}" 1)"
        ;;
      *)
        rest+=("$1")
        ;;
    esac
    shift
  done
  parse_args "${rest[@]}"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
}

split_ids() {
  local object_id_variable="$1" app_id_variable="$2" ids="$3"
  printf -v "$object_id_variable" '%s' "${ids%%$'\t'*}"
  printf -v "$app_id_variable" '%s' "${ids#*$'\t'}"
}

require_existing_app() {
  local display_name="$1"
  local found
  found="$(find_app "$display_name")" || exit 1
  [[ -n "$found" ]] || die "app registration '$display_name' does not exist; run: bash scripts/azure/entra.sh"
  printf '%s' "$found"
}

sentinel_bump_command() {
  local label="$1"
  printf 'az appconfig kv set --name %s --auth-mode login --key %s --label %s --value "$(date -u +%%Y-%%m-%%dT%%H:%%M:%%SZ)" --yes' \
    "$ERP_AZURE_APPCONFIG_NAME" "$SENTINEL_KEY" "$label"
}

converge_signin_app() {
  local label="$1" operator_id="$2" admin_role_id="$3" app_id_variable="$4"
  local display_name vault common_name
  display_name="$(signin_app_name "$label")"
  vault="$(signin_vault "$label")"
  common_name="$(signin_certificate_common_name "$label")"
  local uris
  uris="$(signin_redirect_uris "$label")" || die "cannot compute the redirect URIs of the $label registration"
  local -a redirect_uris=()
  local uri
  while IFS= read -r uri; do
    [[ -n "$uri" ]] || continue
    redirect_uris+=("$uri")
  done <<< "$uris"

  log_step "Sign-in registration '$display_name' ($label)"
  if [[ "$label" == production ]] && (( ${#redirect_uris[@]} == 0 )); then
    log_warn "ERP_AZURE_PRODUCTION_WEB_ORIGIN is empty: no redirect URI is registered and sign-in stays impossible until the parameter is set and entra.sh re-run"
  fi
  local ids object_id app_id
  ids="$(ensure_app "$display_name" web "${redirect_uris[@]}")"
  split_ids object_id app_id "$ids"
  ensure_app_roles "$object_id"
  ensure_required_resource_access "$object_id"
  local sp_id
  sp_id="$(ensure_service_principal "$app_id")"
  ensure_assignment_required "$sp_id"
  ensure_permission_grant "$app_id" "$sp_id"
  local thumbprint
  thumbprint="$(ensure_certificate "$vault" "$SIGNIN_CERTIFICATE_NAME" "$common_name" "$SIGNIN_CERTIFICATE_CONTENT_TYPE")"
  ensure_key_credential "$app_id" "$vault" "$SIGNIN_CERTIFICATE_NAME" "$thumbprint"
  ensure_app_role_assignment "$sp_id" "$operator_id" "$admin_role_id"
  ensure_kv "$IDENTITY_TENANT_ID_KEY" "$label" "$ERP_AZURE_TENANT_ID"
  ensure_kv "$IDENTITY_CLIENT_ID_KEY" "$label" "$app_id"
  printf -v "$app_id_variable" '%s' "$app_id"
}

converge_runtime_app() {
  local app_id_variable="$1"
  log_step "Runtime registration '$ERP_AZURE_APP_RUNTIME_NAME'"
  local ids object_id app_id
  ids="$(ensure_app "$ERP_AZURE_APP_RUNTIME_NAME" none)"
  split_ids object_id app_id "$ids"
  ensure_service_principal "$app_id" > /dev/null
  local thumbprint
  thumbprint="$(ensure_certificate "$ERP_AZURE_KEYVAULT_PROD_NAME" "$RUNTIME_CERTIFICATE_NAME" "$RUNTIME_CERTIFICATE_COMMON_NAME" "$RUNTIME_CERTIFICATE_CONTENT_TYPE")"
  ensure_key_credential "$app_id" "$ERP_AZURE_KEYVAULT_PROD_NAME" "$RUNTIME_CERTIFICATE_NAME" "$thumbprint"
  printf -v "$app_id_variable" '%s' "$app_id"
}

run_converge() {
  require_origin ERP_AZURE_LOCAL_WEB_ORIGIN "$ERP_AZURE_LOCAL_WEB_ORIGIN"
  require_origin ERP_AZURE_LOCAL_API_ORIGIN "$ERP_AZURE_LOCAL_API_ORIGIN"
  [[ -z "$ERP_AZURE_PRODUCTION_WEB_ORIGIN" ]] || require_origin ERP_AZURE_PRODUCTION_WEB_ORIGIN "$ERP_AZURE_PRODUCTION_WEB_ORIGIN"
  log_step "Resolving the operator and Microsoft Graph"
  local operator_id admin_role_id
  operator_id="$(signed_in_user_object_id)"
  log_info "operator: $operator_id"
  resolve_graph_scope_ids
  log_info "Microsoft Graph service principal: $GRAPH_SP_OBJECT_ID"
  admin_role_id="$(app_role_id "$APP_ROLE_ADMIN_VALUE")"

  local local_dev_app_id='' production_app_id='' runtime_app_id=''
  converge_signin_app local-dev "$operator_id" "$admin_role_id" local_dev_app_id
  converge_signin_app production "$operator_id" "$admin_role_id" production_app_id
  converge_runtime_app runtime_app_id

  log_step "Summary"
  printf 'LOCAL_DEV_CLIENT_ID=%s\n' "$local_dev_app_id"
  printf 'PRODUCTION_CLIENT_ID=%s\n' "$production_app_id"
  printf 'RUNTIME_CLIENT_ID=%s\n' "$runtime_app_id"
  printf 'AZURE_TENANT_ID=%s\n' "$ERP_AZURE_TENANT_ID"
  printf 'AZURE_CLIENT_ID=%s\n' "$runtime_app_id"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed; ids shown as $PENDING_ID are assigned by the real run"
  fi
  log_info "AZURE_TENANT_ID and AZURE_CLIENT_ID are the values the server puts in infra/compose/.env"
  log_info "next steps:"
  log_info "  1. bash scripts/azure/provision.sh                   (grants the runtime service principal its roles)"
  log_info "  2. bash scripts/azure/seed.sh                        (arrives with sub-phase azure-configuration-configuration-conventions-and-seed-data)"
  log_info "  3. bash scripts/azure/export-runtime-certificate.sh  (when the server exists)"
  log_info "  4. bash scripts/azure/verify.sh --entra"

  if (( ${#PERMISSION_GRANT_FAILURES[@]} > 0 )); then
    local app_id
    for app_id in "${PERMISSION_GRANT_FAILURES[@]}"; do
      log_error "Graph consent is missing for $app_id; as an administrator run: az ad app permission admin-consent --id $app_id"
    done
    die "${#PERMISSION_GRANT_FAILURES[@]} consent grant(s) failed; every other step completed"
  fi
}

run_rotate_signin() {
  local label="$TARGET"
  local display_name vault common_name
  display_name="$(signin_app_name "$label")"
  vault="$(signin_vault "$label")"
  common_name="$(signin_certificate_common_name "$label")"

  log_step "Rotating the sign-in certificate of '$display_name' ($label)"
  local ids object_id app_id
  ids="$(require_existing_app "$display_name")"
  split_ids object_id app_id "$ids"
  local thumbprint
  thumbprint="$(rotate_certificate "$vault" "$SIGNIN_CERTIFICATE_NAME" "$common_name" "$SIGNIN_CERTIFICATE_CONTENT_TYPE")"
  ensure_key_credential "$app_id" "$vault" "$SIGNIN_CERTIFICATE_NAME" "$thumbprint"

  log_step "Summary"
  printf 'CLIENT_ID=%s\n' "$app_id"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed"
  fi
  log_info "next steps:"
  log_info "  1. bump the $label sentinel so the API reloads the certificate reference:"
  log_info "     $(sentinel_bump_command "$label")"
  log_info "  2. sign in through the $label web app and confirm it works"
  log_info "  3. bash scripts/azure/entra.sh --prune-old-credentials $label"
}

run_rotate_runtime() {
  log_step "Rotating the runtime certificate of '$ERP_AZURE_APP_RUNTIME_NAME'"
  local ids object_id app_id
  ids="$(require_existing_app "$ERP_AZURE_APP_RUNTIME_NAME")"
  split_ids object_id app_id "$ids"
  local thumbprint
  thumbprint="$(rotate_certificate "$ERP_AZURE_KEYVAULT_PROD_NAME" "$RUNTIME_CERTIFICATE_NAME" "$RUNTIME_CERTIFICATE_COMMON_NAME" "$RUNTIME_CERTIFICATE_CONTENT_TYPE")"
  ensure_key_credential "$app_id" "$ERP_AZURE_KEYVAULT_PROD_NAME" "$RUNTIME_CERTIFICATE_NAME" "$thumbprint"

  log_step "Summary"
  printf 'AZURE_CLIENT_ID=%s\n' "$app_id"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed"
  fi
  log_info "next steps:"
  log_info "  1. bash scripts/azure/export-runtime-certificate.sh   (downloads the new version and prints the placement command for the server)"
  log_info "  2. on the server: docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up -d api"
  log_info "  3. bash scripts/azure/verify.sh --entra"
  log_info "  4. bash scripts/azure/entra.sh --prune-old-credentials $RUNTIME_TARGET"
}

run_prune() {
  local display_name vault certificate_name
  if [[ "$TARGET" == "$RUNTIME_TARGET" ]]; then
    display_name="$ERP_AZURE_APP_RUNTIME_NAME"
    vault="$ERP_AZURE_KEYVAULT_PROD_NAME"
    certificate_name="$RUNTIME_CERTIFICATE_NAME"
  else
    display_name="$(signin_app_name "$TARGET")"
    vault="$(signin_vault "$TARGET")"
    certificate_name="$SIGNIN_CERTIFICATE_NAME"
  fi

  log_step "Pruning old key credentials of '$display_name' ($TARGET)"
  local ids object_id app_id
  ids="$(require_existing_app "$display_name")"
  split_ids object_id app_id "$ids"
  local thumbprint
  thumbprint="$(certificate_thumbprint "$vault" "$certificate_name")" || die "cannot read certificate '$certificate_name' in vault '$vault'"
  [[ -n "$thumbprint" ]] || die "certificate '$certificate_name' does not exist in vault '$vault'; run: bash scripts/azure/entra.sh"
  log_info "current certificate thumbprint: $thumbprint"
  prune_key_credentials "$app_id" "$thumbprint"

  log_step "Summary"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was deleted"
  fi
  log_info "next step: bash scripts/azure/verify.sh --entra"
}

main() {
  parse_entra_args "$@"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az node base64 od mktemp

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  case "$MODE" in
    converge) run_converge ;;
    rotate-signin) run_rotate_signin ;;
    rotate-runtime) run_rotate_runtime ;;
    prune) run_prune ;;
    *) die "unknown mode '$MODE'" ;;
  esac
}

main "$@"
