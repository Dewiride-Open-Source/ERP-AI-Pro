#!/usr/bin/env bash

if [[ -n "${ERP_AZURE_COMMON_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_AZURE_COMMON_SH_LOADED=1

set -Eeuo pipefail

export MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
readonly SCRIPT_DIR REPO_ROOT

DRY_RUN="${DRY_RUN:-0}"
PARAMS_FILE="${PARAMS_FILE:-$SCRIPT_DIR/params.env}"
ACCEPTS_DRY_RUN="${ACCEPTS_DRY_RUN:-1}"
ACCEPTS_PARAMS="${ACCEPTS_PARAMS:-1}"
USAGE="${USAGE:-}"
ARGS=()

readonly ERP_AZURE_REQUIRED_PARAMS=(
  ERP_AZURE_TENANT_ID
  ERP_AZURE_SUBSCRIPTION_ID
  ERP_AZURE_LOCATION
  ERP_AZURE_RESOURCE_GROUP
  ERP_AZURE_APPCONFIG_NAME
  ERP_AZURE_KEYVAULT_DEV_NAME
  ERP_AZURE_KEYVAULT_PROD_NAME
)

readonly ERP_AZURE_APPCONFIG_SKUS=(Free Developer Standard Premium)

log_step() { printf '\n==> %s\n' "$*" >&2; }
log_info() { printf '    %s\n' "$*" >&2; }
log_warn() { printf '[warn] %s\n' "$*" >&2; }
log_error() { printf '[error] %s\n' "$*" >&2; }

die() {
  log_error "$*"
  exit 1
}

require_command() {
  local name
  for name in "$@"; do
    command -v "$name" > /dev/null 2>&1 || die "required command '$name' is not on PATH"
  done
}

require_var() {
  local name
  for name in "$@"; do
    [[ -n "${!name:-}" ]] || die "parameter $name is required; set it in $PARAMS_FILE"
  done
}

is_guid() {
  [[ "$1" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]
}

quote_command() {
  local quoted
  printf -v quoted '%q ' "$@"
  printf '%s' "${quoted% }"
}

parse_args() {
  ARGS=()
  while (( $# > 0 )); do
    case "$1" in
      --dry-run)
        (( ACCEPTS_DRY_RUN )) || die "unknown option '$1'"
        DRY_RUN=1
        ;;
      --params)
        (( ACCEPTS_PARAMS )) || die "unknown option '$1'"
        [[ -n "${2:-}" ]] || die "--params requires a file path"
        PARAMS_FILE="$2"
        shift
        ;;
      --params=*)
        (( ACCEPTS_PARAMS )) || die "unknown option '--params'"
        PARAMS_FILE="${1#--params=}"
        [[ -n "$PARAMS_FILE" ]] || die "--params requires a file path"
        ;;
      --help | -h)
        printf '%s\n' "$USAGE"
        exit 0
        ;;
      --)
        shift
        ARGS+=("$@")
        break
        ;;
      -*)
        die "unknown option '$1'"
        ;;
      *)
        ARGS+=("$1")
        ;;
    esac
    shift
  done
}

load_params() {
  [[ -f "$PARAMS_FILE" ]] || die "parameter file '$PARAMS_FILE' not found; copy $SCRIPT_DIR/params.env.example to $SCRIPT_DIR/params.env and fill it in"

  local line key value line_number=0
  while IFS= read -r line || [[ -n "$line" ]]; do
    line_number=$((line_number + 1))
    line="${line%$'\r'}"
    [[ "$line" =~ ^[[:space:]]*(#|$) ]] && continue
    [[ "$line" == *=* ]] || die "$PARAMS_FILE:$line_number: expected KEY=VALUE"
    key="${line%%=*}"
    value="${line#*=}"
    key="${key#"${key%%[![:space:]]*}"}"
    key="${key%"${key##*[![:space:]]}"}"
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"
    [[ "$key" =~ ^ERP_AZURE_[A-Z0-9_]+$ ]] || die "$PARAMS_FILE:$line_number: key '$key' must match ERP_AZURE_<NAME>"
    if [[ "$value" =~ ^\"(.*)\"$ ]] || [[ "$value" =~ ^\'(.*)\'$ ]]; then
      value="${BASH_REMATCH[1]}"
    fi
    printf -v "$key" '%s' "$value"
    export "${key?}"
  done < "$PARAMS_FILE"

  : "${ERP_AZURE_APPCONFIG_SKU:=Free}"
  : "${ERP_AZURE_DEVELOPERS_GROUP:=}"
  : "${ERP_AZURE_PRODUCTION_WEB_ORIGIN:=}"
  : "${ERP_AZURE_LOCAL_WEB_ORIGIN:=http://localhost:3000}"
  : "${ERP_AZURE_LOCAL_API_ORIGIN:=http://localhost:5080}"
  : "${ERP_AZURE_APP_WEB_LOCAL_DEV_NAME:=ERP-AI-Pro Web (local-dev)}"
  : "${ERP_AZURE_APP_WEB_PRODUCTION_NAME:=ERP-AI-Pro Web (production)}"
  : "${ERP_AZURE_APP_RUNTIME_NAME:=ERP-AI-Pro Runtime (production)}"
  export ERP_AZURE_APPCONFIG_SKU ERP_AZURE_DEVELOPERS_GROUP ERP_AZURE_PRODUCTION_WEB_ORIGIN \
    ERP_AZURE_LOCAL_WEB_ORIGIN ERP_AZURE_LOCAL_API_ORIGIN ERP_AZURE_APP_WEB_LOCAL_DEV_NAME \
    ERP_AZURE_APP_WEB_PRODUCTION_NAME ERP_AZURE_APP_RUNTIME_NAME

  require_var "${ERP_AZURE_REQUIRED_PARAMS[@]}"
  is_guid "$ERP_AZURE_TENANT_ID" || die "ERP_AZURE_TENANT_ID must be a GUID"
  is_guid "$ERP_AZURE_SUBSCRIPTION_ID" || die "ERP_AZURE_SUBSCRIPTION_ID must be a GUID"

  local sku
  for sku in "${ERP_AZURE_APPCONFIG_SKUS[@]}"; do
    [[ "$ERP_AZURE_APPCONFIG_SKU" == "$sku" ]] && return 0
  done
  die "ERP_AZURE_APPCONFIG_SKU must be one of: ${ERP_AZURE_APPCONFIG_SKUS[*]}"
}

az_read() {
  az "$@" --only-show-errors | tr -d '\r'
}

ensure_login() {
  local account tenant_id subscription_id
  if ! account="$(az_read account show --query '[tenantId, id]' --output tsv 2> /dev/null)"; then
    die "not signed in to Azure; run: az login --tenant $ERP_AZURE_TENANT_ID"
  fi
  tenant_id="$(printf '%s\n' "$account" | sed -n '1p')"
  subscription_id="$(printf '%s\n' "$account" | sed -n '2p')"
  if [[ "${tenant_id,,}" != "${ERP_AZURE_TENANT_ID,,}" ]]; then
    die "signed in to tenant $tenant_id but ERP_AZURE_TENANT_ID is $ERP_AZURE_TENANT_ID; run: az login --tenant $ERP_AZURE_TENANT_ID"
  fi
  if [[ "${subscription_id,,}" != "${ERP_AZURE_SUBSCRIPTION_ID,,}" ]]; then
    die "active subscription is $subscription_id but ERP_AZURE_SUBSCRIPTION_ID is $ERP_AZURE_SUBSCRIPTION_ID; run: az account set --subscription $ERP_AZURE_SUBSCRIPTION_ID"
  fi
  log_info "signed in to tenant $tenant_id, subscription $subscription_id"
}

signed_in_user_object_id() {
  local object_id
  object_id="$(az_read ad signed-in-user show --query id --output tsv)" || die "cannot read the signed-in user; run: az login --tenant $ERP_AZURE_TENANT_ID"
  [[ -n "$object_id" ]] || die "the signed-in identity has no user object id (a service principal cannot run this script)"
  printf '%s' "$object_id"
}

developers_group_object_id() {
  if [[ -z "$ERP_AZURE_DEVELOPERS_GROUP" ]]; then
    return 0
  fi
  local object_id
  if ! object_id="$(az_read ad group show --group "$ERP_AZURE_DEVELOPERS_GROUP" --query id --output tsv 2> /dev/null)" || [[ -z "$object_id" ]]; then
    die "Entra group '$ERP_AZURE_DEVELOPERS_GROUP' not found; create the group first or leave the parameter empty"
  fi
  printf '%s' "$object_id"
}

runtime_service_principal_object_id() {
  local object_id
  object_id="$(az_read ad sp list --display-name "$ERP_AZURE_APP_RUNTIME_NAME" --query '[0].id' --output tsv)" || die "cannot list service principals named '$ERP_AZURE_APP_RUNTIME_NAME'"
  printf '%s' "$object_id"
}

run() {
  if (( DRY_RUN )); then
    printf '[dry-run] %s\n' "$(quote_command "$@")" >&2
    return 0
  fi
  "$@"
}

retry() {
  local attempts=20 delay=15
  if [[ "${1:-}" != "--" ]]; then
    (( $# >= 3 )) || die "retry: usage retry [<attempts> <delay-seconds>] -- <command...>"
    attempts="$1"
    delay="$2"
    shift 2
  fi
  [[ "${1:-}" == "--" ]] || die "retry: expected '--' before the command"
  shift
  (( $# > 0 )) || die "retry: no command given"

  local attempt=1
  while true; do
    if "$@"; then
      return 0
    fi
    if (( attempt >= attempts )); then
      die "'$(quote_command "$@")' failed after $attempts attempts; role assignments can take up to 15 minutes to propagate; re-run"
    fi
    log_warn "attempt $attempt of $attempts failed; retrying in ${delay}s"
    sleep "$delay"
    attempt=$((attempt + 1))
  done
}

store_resource_id() {
  printf '/subscriptions/%s/resourceGroups/%s/providers/Microsoft.AppConfiguration/configurationStores/%s' \
    "$ERP_AZURE_SUBSCRIPTION_ID" "$ERP_AZURE_RESOURCE_GROUP" "$ERP_AZURE_APPCONFIG_NAME"
}

vault_resource_id() {
  local vault_name="$1"
  printf '/subscriptions/%s/resourceGroups/%s/providers/Microsoft.KeyVault/vaults/%s' \
    "$ERP_AZURE_SUBSCRIPTION_ID" "$ERP_AZURE_RESOURCE_GROUP" "$vault_name"
}

utc_timestamp() {
  date -u +%Y-%m-%dT%H:%M:%SZ
}

json_field() {
  local json="$1" path="$2"
  node -e 'let raw = ""; process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => { let value = JSON.parse(raw); for (const segment of process.argv[1].split(".")) { value = value == null ? undefined : value[segment]; } process.stdout.write(value == null ? "" : String(value)); });' "$path" <<< "$json"
}
