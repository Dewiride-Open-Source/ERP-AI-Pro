#!/usr/bin/env bash

if [[ -n "${ERP_AZURE_SQL_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_AZURE_SQL_SH_LOADED=1

# shellcheck source=common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"

readonly SQL_API_VERSION='2025-01-01'
readonly SQL_FIREWALL_RULE_NAME='home'
readonly SQL_SERVER_LOCK_NAME='do-not-delete'
readonly SQL_AUDIT_APPEND_ONLY_ROLE='erp_audit_append_only'
readonly SQL_USERS_SCRIPT='scripts/azure/sql/database-users.sql'
readonly SQL_CONNECTION_STRING_SECRETS=(Erp--Platform--Database--ConnectionString Erp--Platform--Database--MigratorConnectionString)
readonly SQL_WINDOWS_GO_SQLCMD='/c/Program Files/SqlCmd/sqlcmd.exe'
readonly SQL_ACCESS_ATTEMPTS=10
readonly SQL_ACCESS_DELAY_SECONDS=30
readonly SQL_HOME_HINT='from a computer on the home network, without a VPN: bash scripts/azure/database.sh --allow-current-address'

SQLCMD_BIN=''
SQL_SERVER_HOST=''

is_go_sqlcmd() {
  local version
  version="$("$1" --version 2> /dev/null | tr -d '\r')" || return 1
  [[ "$version" == *'Version: v'* ]]
}

# The ODBC sqlcmd that SQL Server installs signs in to Microsoft Entra ID only through a window of its
# own, so every query here runs through the Go sqlcmd, which reuses the Azure CLI sign-in.
resolve_sqlcmd() {
  local install_hint='install it (Windows: winget install sqlcmd; macOS: brew install sqlcmd; https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-download-install)'
  if [[ -n "$ERP_AZURE_SQLCMD" ]]; then
    is_go_sqlcmd "$ERP_AZURE_SQLCMD" || die "ERP_AZURE_SQLCMD '$ERP_AZURE_SQLCMD' is not the Go sqlcmd; $install_hint"
    SQLCMD_BIN="$ERP_AZURE_SQLCMD"
    return 0
  fi
  local on_path
  on_path="$(command -v sqlcmd 2> /dev/null || true)"
  if [[ -n "$on_path" ]] && is_go_sqlcmd "$on_path"; then
    SQLCMD_BIN="$on_path"
  elif [[ -x "$SQL_WINDOWS_GO_SQLCMD" ]] && is_go_sqlcmd "$SQL_WINDOWS_GO_SQLCMD"; then
    SQLCMD_BIN="$SQL_WINDOWS_GO_SQLCMD"
  else
    die "the Go sqlcmd was not found (the sqlcmd on PATH, if any, is the ODBC one); $install_hint, or set ERP_AZURE_SQLCMD to its path"
  fi
  log_info "sqlcmd: $SQLCMD_BIN"
}

sql_server_resource_id() {
  printf '/subscriptions/%s/resourceGroups/%s/providers/Microsoft.Sql/servers/%s' \
    "$ERP_AZURE_SUBSCRIPTION_ID" "$ERP_AZURE_RESOURCE_GROUP" "$ERP_AZURE_SQL_SERVER_NAME"
}

# The api and the migrator share the string: each signs in as the identity its container's environment names.
database_connection_string() {
  local server_fqdn="$1"
  printf 'Server=tcp:%s,1433;Database=%s;Authentication="Active Directory Default";Encrypt=True;TrustServerCertificate=False;Connect Timeout=30' \
    "$server_fqdn" "$ERP_AZURE_SQL_DATABASE_NAME"
}

sql_server_exists() {
  local output
  if output="$(az_read sql server show --name "$ERP_AZURE_SQL_SERVER_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" --query name --output tsv 2>&1)"; then
    return 0
  fi
  [[ "$output" == *ResourceNotFound* || "$output" == *'was not found'* ]] \
    || die "cannot read the server $ERP_AZURE_SQL_SERVER_NAME: $output"
  return 1
}

resolve_sql_server_host() {
  SQL_SERVER_HOST="$(az_read sql server show --name "$ERP_AZURE_SQL_SERVER_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query fullyQualifiedDomainName --output tsv)" || die "cannot read the server $ERP_AZURE_SQL_SERVER_NAME; run: bash scripts/azure/provision.sh"
  [[ -n "$SQL_SERVER_HOST" ]] || die "the server $ERP_AZURE_SQL_SERVER_NAME has no host name"
}

firewall_rules_tsv() {
  az_read sql server firewall-rule list --resource-group "$ERP_AZURE_RESOURCE_GROUP" --server "$ERP_AZURE_SQL_SERVER_NAME" \
    --query '[].[name, startIpAddress, endIpAddress]' --output tsv
}

home_rule_address() {
  local rules name start end
  rules="$(firewall_rules_tsv)" || die "cannot list the firewall rules of $ERP_AZURE_SQL_SERVER_NAME"
  while IFS=$'\t' read -r name start end; do
    if [[ "$name" == "$SQL_FIREWALL_RULE_NAME" && "$start" == "$end" ]]; then
      printf '%s' "$start"
    fi
  done <<< "$rules"
}

sql_run() {
  local database="$1"
  shift
  "$SQLCMD_BIN" -S "tcp:$SQL_SERVER_HOST,1433" -d "$database" --authentication-method ActiveDirectoryAzCli -N true -b -l 30 "$@" 2>&1 \
    | tr -d '\r'
}

run_users_script() {
  local dry_run="$1" runtime_app_id="$2" migrator_app_id="$3"
  sql_run "$ERP_AZURE_SQL_DATABASE_NAME" -i "$SQL_USERS_SCRIPT" \
    -v "RuntimeUser=$ERP_AZURE_APP_RUNTIME_NAME" -v "RuntimeAppId=$runtime_app_id" \
    -v "MigratorUser=$ERP_AZURE_APP_MIGRATOR_NAME" -v "MigratorAppId=$migrator_app_id" \
    -v "AuditRole=$SQL_AUDIT_APPEND_ONLY_ROLE" -v "DryRun=$dry_run"
}

refused_client_address() {
  local output="$1"
  [[ "$output" =~ Client\ with\ IP\ address\ \'([0-9.]+)\'\ is\ not\ allowed ]] || return 1
  printf '%s' "${BASH_REMATCH[1]}"
}

first_value() {
  local line
  while IFS= read -r line; do
    if [[ -n "${line//[[:space:]]/}" ]]; then
      printf '%s' "${line//[[:space:]]/}"
      return 0
    fi
  done <<< "$1"
}

probe_sql_access() {
  local output address
  if output="$(sql_run master -h -1 -W -Q 'SET NOCOUNT ON; SELECT client_net_address FROM sys.dm_exec_connections WHERE session_id = @@SPID;')"; then
    printf 'answered %s' "$(first_value "$output")"
  elif address="$(refused_client_address "$output")"; then
    printf 'refused %s' "$address"
  elif [[ "$output" == *'Login failed'* ]]; then
    printf 'signin-refused %s' "$(printf '%s' "$output" | tr '\n' ' ')"
  else
    printf 'unreachable %s' "$(printf '%s' "$output" | tr '\n' ' ')"
  fi
}

# A firewall rule takes up to five minutes to apply, so a refusal of the address the rule already names is waited out;
# any other refusal is a changed home address, which no wait can fix.
wait_for_sql_access() {
  local attempts="$1"
  local attempt=1 result home_address
  while true; do
    result="$(probe_sql_access)"
    case "$result" in
      answered*) return 0 ;;
      refused*) ;;
      signin-refused*) die "the server refused the sign-in of $(az_read account show --query user.name --output tsv): ${result#signin-refused }" ;;
      *) die "cannot reach $SQL_SERVER_HOST: ${result#unreachable }" ;;
    esac
    home_address="$(home_rule_address)"
    if [[ "${result#refused }" != "$home_address" ]]; then
      die "the firewall refuses this computer's address ${result#refused }, and its rule $SQL_FIREWALL_RULE_NAME admits ${home_address:-no single address}; run $SQL_HOME_HINT"
    fi
    if (( attempt >= attempts )); then
      die "the firewall of $ERP_AZURE_SQL_SERVER_NAME still refuses ${result#refused } after $attempts attempts"
    fi
    log_warn "the firewall refuses ${result#refused } (attempt $attempt of $attempts; the rule changed within the last five minutes and is still applying); retrying in ${SQL_ACCESS_DELAY_SECONDS}s"
    sleep "$SQL_ACCESS_DELAY_SECONDS"
    attempt=$((attempt + 1))
  done
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

# Azure sees the home's public address only, so a private, shared or reserved one is a typing error.
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
