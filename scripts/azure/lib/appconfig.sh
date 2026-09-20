#!/usr/bin/env bash

if [[ -n "${ERP_AZURE_APPCONFIG_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_AZURE_APPCONFIG_SH_LOADED=1

# shellcheck source=common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"

readonly APPCONFIG_KEYVAULT_REFERENCE_CONTENT_TYPE='application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8'
readonly APPCONFIG_NULL_LABEL_FILTER='\0'

appconfig_label_display() {
  local label="$1"
  if [[ -n "$label" ]]; then
    printf '%s' "$label"
  else
    printf '%s' '(null)'
  fi
}

appconfig_kv_query() {
  local key="$1" label="$2" field="$3"
  local label_filter="$label"
  [[ -n "$label_filter" ]] || label_filter="$APPCONFIG_NULL_LABEL_FILTER"
  az appconfig kv list --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --key "$key" --label "$label_filter" \
    --query "[0].$field" --output tsv --only-show-errors | tr -d '\r'
}

appconfig_kv_get() {
  (( $# == 2 )) || die "appconfig_kv_get: usage appconfig_kv_get <key> <label>"
  appconfig_kv_query "$1" "$2" 'value'
}

appconfig_kv_reference_uri() {
  local key="$1" label="$2"
  local value
  value="$(appconfig_kv_query "$key" "$label" 'value')" || return 1
  [[ -n "$value" ]] || return 0
  node -e 'let raw = ""; process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => { let uri = ""; try { const parsed = JSON.parse(raw); if (typeof parsed.uri === "string") uri = parsed.uri; } catch { uri = ""; } process.stdout.write(uri); });' <<< "$value"
}

appconfig_read_current() {
  local -n target="$1"
  shift
  if target="$("$@")"; then
    return 0
  fi
  (( DRY_RUN )) || return 1
  log_warn "store '$ERP_AZURE_APPCONFIG_NAME' could not be read; the dry run assumes the key is absent"
  target=""
}

appconfig_log_write() {
  local key="$1" label="$2" previous="$3" suffix="$4"
  local verb="updated"
  [[ -n "$previous" ]] || verb="created"
  (( DRY_RUN )) && verb="would be $verb"
  log_info "$key [$(appconfig_label_display "$label")] $verb$suffix"
}

ensure_kv_once() {
  local key="$1" label="$2" value="$3"
  local current
  appconfig_read_current current appconfig_kv_get "$key" "$label" || return 1
  if [[ -n "$current" && "$current" == "$value" ]]; then
    log_info "$key [$(appconfig_label_display "$label")] unchanged"
    return 0
  fi
  local -a label_args=()
  [[ -z "$label" ]] || label_args=(--label "$label")
  run az appconfig kv set --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --key "$key" "${label_args[@]}" \
    --value "$value" --yes --output none --only-show-errors || return 1
  appconfig_log_write "$key" "$label" "$current" ""
}

ensure_kv() {
  (( $# == 3 )) || die "ensure_kv: usage ensure_kv <key> <label> <value>"
  [[ -n "$3" ]] || die "ensure_kv: the value for '$1' must not be empty"
  retry -- ensure_kv_once "$1" "$2" "$3"
}

ensure_kv_reference_once() {
  local key="$1" label="$2" secret_uri="$3"
  local current_content_type current_uri
  appconfig_read_current current_content_type appconfig_kv_query "$key" "$label" 'contentType' || return 1
  appconfig_read_current current_uri appconfig_kv_reference_uri "$key" "$label" || return 1
  if [[ "$current_content_type" == "$APPCONFIG_KEYVAULT_REFERENCE_CONTENT_TYPE" && "$current_uri" == "$secret_uri" ]]; then
    log_info "$key [$(appconfig_label_display "$label")] unchanged (Key Vault reference)"
    return 0
  fi
  local -a label_args=()
  [[ -z "$label" ]] || label_args=(--label "$label")
  run az appconfig kv set-keyvault --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --key "$key" "${label_args[@]}" \
    --secret-identifier "$secret_uri" --yes --output none --only-show-errors || return 1
  appconfig_log_write "$key" "$label" "$current_content_type$current_uri" " (Key Vault reference)"
}

ensure_kv_reference() {
  (( $# == 3 )) || die "ensure_kv_reference: usage ensure_kv_reference <key> <label> <secret-uri>"
  retry -- ensure_kv_reference_once "$1" "$2" "$3"
}
