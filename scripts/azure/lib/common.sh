#!/usr/bin/env bash

if [[ -n "${ERP_AZURE_COMMON_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_AZURE_COMMON_SH_LOADED=1

set -Eeuo pipefail
shopt -s inherit_errexit

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
readonly ERP_AZURE_TEMP_DIR='scripts/azure/out'
readonly ERP_AZURE_MIGRATOR_SECRETS=(Erp--Platform--Database--ConnectionString Erp--Platform--Database--MigratorConnectionString)
readonly HOME_ADDRESS_HINT='from a computer on the home network, without a VPN: bash scripts/azure/home-address.sh'

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

ipv4_octets_valid() {
  local address="$1"
  [[ "$address" =~ ^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$ ]] || return 1
  local octet
  for octet in "${BASH_REMATCH[@]:1}"; do
    [[ "$octet" == 0 || "$octet" != 0* ]] || return 1
    (( octet <= 255 )) || return 1
  done
}

is_public_ipv4() {
  local address="$1"
  ipv4_octets_valid "$address" || return 1
  local a b
  IFS=. read -r a b _ _ <<< "$address"
  (( a != 0 && a != 10 && a != 127 && a < 224 )) || return 1
  (( !(a == 100 && b >= 64 && b <= 127) )) || return 1
  (( !(a == 169 && b == 254) )) || return 1
  (( !(a == 172 && b >= 16 && b <= 31) )) || return 1
  (( !(a == 192 && b == 168) )) || return 1
  (( !(a == 198 && (b == 18 || b == 19)) )) || return 1
  case "$address" in
    192.0.0.* | 192.0.2.* | 198.51.100.* | 203.0.113.*) return 1 ;;
  esac
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
  : "${ERP_AZURE_APP_WEB_LOCAL_DEV_NAME:=ERP-AI-Pro Web (local-dev)}"
  : "${ERP_AZURE_APP_WEB_PRODUCTION_NAME:=ERP-AI-Pro Web (production)}"
  : "${ERP_AZURE_APP_RUNTIME_NAME:=ERP-AI-Pro Runtime (production)}"
  : "${ERP_AZURE_APP_MIGRATOR_NAME:=ERP-AI-Pro Migrator (production)}"
  : "${ERP_AZURE_APP_BACKUP_NAME:=ERP-AI-Pro Backup (production)}"
  : "${ERP_AZURE_STORAGE_DEV_NAME:=sterpaiprodev}"
  : "${ERP_AZURE_STORAGE_PROD_NAME:=sterpaiproprod}"
  : "${ERP_AZURE_STORAGE_BACKUP_NAME:=sterpaiprobackup}"
  : "${ERP_AZURE_KEYVAULT_OPS_NAME:=kv-erp-ai-pro-ops}"
  : "${ERP_AZURE_SQL_SERVER_NAME:=sql-erp-ai-pro}"
  : "${ERP_AZURE_SQL_DATABASE_NAME:=ErpAiPro}"
  : "${ERP_AZURE_SQLCMD:=}"
  export ERP_AZURE_APPCONFIG_SKU ERP_AZURE_DEVELOPERS_GROUP ERP_AZURE_PRODUCTION_WEB_ORIGIN \
    ERP_AZURE_LOCAL_WEB_ORIGIN ERP_AZURE_APP_WEB_LOCAL_DEV_NAME \
    ERP_AZURE_APP_WEB_PRODUCTION_NAME ERP_AZURE_APP_RUNTIME_NAME ERP_AZURE_APP_MIGRATOR_NAME ERP_AZURE_APP_BACKUP_NAME \
    ERP_AZURE_STORAGE_DEV_NAME ERP_AZURE_STORAGE_PROD_NAME ERP_AZURE_STORAGE_BACKUP_NAME ERP_AZURE_KEYVAULT_OPS_NAME \
    ERP_AZURE_SQL_SERVER_NAME ERP_AZURE_SQL_DATABASE_NAME ERP_AZURE_SQLCMD

  require_var "${ERP_AZURE_REQUIRED_PARAMS[@]}"
  is_guid "$ERP_AZURE_TENANT_ID" || die "ERP_AZURE_TENANT_ID must be a GUID"
  is_guid "$ERP_AZURE_SUBSCRIPTION_ID" || die "ERP_AZURE_SUBSCRIPTION_ID must be a GUID"
  local storage_parameter
  for storage_parameter in ERP_AZURE_STORAGE_DEV_NAME ERP_AZURE_STORAGE_PROD_NAME ERP_AZURE_STORAGE_BACKUP_NAME; do
    [[ "${!storage_parameter}" =~ ^[a-z0-9]{3,24}$ ]] \
      || die "$storage_parameter must be 3 to 24 lowercase letters or digits; '${!storage_parameter}' was rejected"
  done
  [[ "$ERP_AZURE_STORAGE_PROD_NAME" != "$ERP_AZURE_STORAGE_DEV_NAME" ]] \
    || die "ERP_AZURE_STORAGE_PROD_NAME and ERP_AZURE_STORAGE_DEV_NAME must name two accounts; both are '$ERP_AZURE_STORAGE_DEV_NAME'"
  [[ "$ERP_AZURE_STORAGE_BACKUP_NAME" != "$ERP_AZURE_STORAGE_DEV_NAME" && "$ERP_AZURE_STORAGE_BACKUP_NAME" != "$ERP_AZURE_STORAGE_PROD_NAME" ]] \
    || die "ERP_AZURE_STORAGE_BACKUP_NAME must name an account of its own, apart from ERP_AZURE_STORAGE_DEV_NAME and ERP_AZURE_STORAGE_PROD_NAME; '$ERP_AZURE_STORAGE_BACKUP_NAME' was rejected"
  [[ "$ERP_AZURE_KEYVAULT_OPS_NAME" =~ ^[a-zA-Z][a-zA-Z0-9-]{1,22}[a-zA-Z0-9]$ ]] \
    || die "ERP_AZURE_KEYVAULT_OPS_NAME must be 3 to 24 letters, digits or hyphens, starting with a letter; '$ERP_AZURE_KEYVAULT_OPS_NAME' was rejected"
  [[ "$ERP_AZURE_SQL_SERVER_NAME" =~ ^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$ ]] \
    || die "ERP_AZURE_SQL_SERVER_NAME must be 1 to 63 lowercase letters, digits or hyphens, starting and ending with a letter or digit; '$ERP_AZURE_SQL_SERVER_NAME' was rejected"
  [[ "$ERP_AZURE_SQL_DATABASE_NAME" =~ ^[A-Za-z0-9_]{1,128}$ ]] \
    || die "ERP_AZURE_SQL_DATABASE_NAME must be 1 to 128 letters, digits or underscores; '$ERP_AZURE_SQL_DATABASE_NAME' was rejected"
  local app_name_parameter
  for app_name_parameter in ERP_AZURE_APP_RUNTIME_NAME ERP_AZURE_APP_MIGRATOR_NAME; do
    [[ "${!app_name_parameter}" != *[\]\'\"]* ]] \
      || die "$app_name_parameter names a database user, so it cannot contain ], ' or \"; '${!app_name_parameter}' was rejected"
  done

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

odata_string_literal() {
  local value="$1"
  printf "'%s'" "${value//\'/\'\'}"
}

developers_group_object_id() {
  if [[ -z "$ERP_AZURE_DEVELOPERS_GROUP" ]]; then
    return 0
  fi
  local ids
  if is_guid "$ERP_AZURE_DEVELOPERS_GROUP"; then
    ids="$(az_read ad group show --group "$ERP_AZURE_DEVELOPERS_GROUP" --query id --output tsv 2> /dev/null)" || ids=''
  else
    ids="$(az_read ad group list --filter "displayName eq $(odata_string_literal "$ERP_AZURE_DEVELOPERS_GROUP")" --query '[].id' --output tsv)" \
      || die "cannot list Entra groups named '$ERP_AZURE_DEVELOPERS_GROUP'"
  fi
  [[ -n "$ids" ]] || die "Entra group '$ERP_AZURE_DEVELOPERS_GROUP' not found; create the group first or leave the parameter empty"
  (( $(printf '%s\n' "$ids" | wc -l) == 1 )) || die "more than one Entra group is named '$ERP_AZURE_DEVELOPERS_GROUP'; set the parameter to the object id of the intended group"
  printf '%s' "$ids"
}

service_principal_lookup() {
  local display_name="$1" field="$2"
  local values
  values="$(az_read ad sp list --filter "displayName eq $(odata_string_literal "$display_name")" --query "[].$field" --output tsv)" \
    || die "cannot list service principals named '$display_name'"
  if [[ -n "$values" ]] && (( $(printf '%s\n' "$values" | wc -l) > 1 )); then
    die "more than one service principal is named '$display_name'; rename or delete the duplicates"
  fi
  printf '%s' "$values"
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
      die "'$(quote_command "$@")' failed after $attempts attempts (see the errors above); a 403 means a role assignment is still propagating, which can take up to 15 minutes: re-run"
    fi
    log_warn "attempt $attempt of $attempts failed; retrying in ${delay}s"
    sleep "$delay"
    attempt=$((attempt + 1))
  done
}

azure_temp_file() {
  local purpose="$1"
  mkdir -p -- "$ERP_AZURE_TEMP_DIR"
  mktemp "$ERP_AZURE_TEMP_DIR/$purpose.XXXXXX" || die "cannot create a temporary file under $ERP_AZURE_TEMP_DIR"
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

storage_account_resource_id() {
  local account_name="$1"
  printf '/subscriptions/%s/resourceGroups/%s/providers/Microsoft.Storage/storageAccounts/%s' \
    "$ERP_AZURE_SUBSCRIPTION_ID" "$ERP_AZURE_RESOURCE_GROUP" "$account_name"
}

storage_container_resource_id() {
  local account_name="$1" container_name="$2"
  printf '%s/blobServices/default/containers/%s' "$(storage_account_resource_id "$account_name")" "$container_name"
}

utc_timestamp() {
  date -u +%Y-%m-%dT%H:%M:%SZ
}

json_field() {
  local json="$1" path="$2"
  node -e 'let raw = ""; process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => { let value = JSON.parse(raw); for (const segment of process.argv[1].split(".")) { value = value == null ? undefined : value[segment]; } process.stdout.write(value == null ? "" : String(value)); });' "$path" <<< "$json"
}

canonical_uri() {
  node -e 'process.stdout.write(new URL(process.argv[1]).href);' "$1" 2> /dev/null
}
