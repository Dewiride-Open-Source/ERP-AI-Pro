#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/database.sh [--dry-run] [--params <file>]
       bash scripts/azure/database.sh --allow-current-address [--dry-run] [--params <file>]
       bash scripts/azure/database.sh --allow-address <IPv4> [--dry-run] [--params <file>]

Converges the users of the production database as its Microsoft Entra admin,
through the Go sqlcmd and the Azure CLI sign-in: the contained users of the
runtime and migrator service principals, each checked against its app'"'"'s
client id; db_ddladmin, the data roles and CREATE ROLE for the migrator; and,
once the migrations have created the schema audit and the role
erp_audit_append_only, the runtime'"'"'s membership of that role and its data
roles, proved under EXECUTE AS USER.

--allow-current-address and --allow-address set the server'"'"'s one firewall rule
instead: to the address Azure sees this computer connect from, or to the one
given. Run --allow-current-address only from a computer on the home network,
without a VPN, because the server at home reaches the database only through that
address. Every mode fails while another firewall rule exists. Safe to run
repeatedly; never deletes a user or a rule.

Options:
  --allow-current-address   Point the firewall rule at the address this computer reaches Azure from.
  --allow-address <IPv4>    Point the firewall rule at this public IPv4 address.
  --dry-run                 Print every change that would be made; change nothing.
  --params <file>           Parameter file (default: scripts/azure/params.env).
  --help                    Show this help.'

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/sql.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/sql.sh"

MODE=users
ALLOW_ADDRESS=''

set_mode() {
  [[ "$MODE" == users ]] || die "give one of --allow-current-address and --allow-address"
  MODE="$1"
}

parse_database_args() {
  local -a rest=()
  while (( $# > 0 )); do
    case "$1" in
      --allow-current-address)
        set_mode current-address
        ;;
      --allow-address)
        set_mode address
        [[ -n "${2:-}" ]] || die "--allow-address requires an IPv4 address"
        ALLOW_ADDRESS="$2"
        shift
        ;;
      --allow-address=*)
        set_mode address
        ALLOW_ADDRESS="${1#--allow-address=}"
        [[ -n "$ALLOW_ADDRESS" ]] || die "--allow-address requires an IPv4 address"
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

lock_hint() {
  printf 'az lock delete --name %s --resource-group %s --resource-type Microsoft.Sql/servers --resource-name %s' \
    "$SQL_SERVER_LOCK_NAME" "$ERP_AZURE_RESOURCE_GROUP" "$ERP_AZURE_SQL_SERVER_NAME"
}

check_firewall_rules() {
  local rules name start end others=0
  rules="$(firewall_rules_tsv)" || die "cannot list the firewall rules of $ERP_AZURE_SQL_SERVER_NAME"
  while IFS=$'\t' read -r name start end; do
    if [[ -z "$name" || "$name" == "$SQL_FIREWALL_RULE_NAME" ]]; then
      continue
    fi
    log_warn "the rule $name ($start to $end) is not this script's; the server's lock keeps it until removed by hand: $(lock_hint), az sql server firewall-rule delete --resource-group $ERP_AZURE_RESOURCE_GROUP --server $ERP_AZURE_SQL_SERVER_NAME --name $name, then bash scripts/azure/provision.sh to lock the server again"
    others=$((others + 1))
  done <<< "$rules"
  (( others == 0 )) || die "$others firewall rule(s) besides $SQL_FIREWALL_RULE_NAME admit other addresses; remove them as shown above"
}

ensure_firewall_rule() {
  local address="$1"
  is_public_ipv4 "$address" || die "'$address' is not a public IPv4 address; Azure sees the home only by its public address"
  log_step "Firewall rule $SQL_FIREWALL_RULE_NAME of $ERP_AZURE_SQL_SERVER_NAME"
  local rules name start end current_start='' current_end=''
  rules="$(firewall_rules_tsv)" || die "cannot list the firewall rules of $ERP_AZURE_SQL_SERVER_NAME"
  while IFS=$'\t' read -r name start end; do
    if [[ "$name" == "$SQL_FIREWALL_RULE_NAME" ]]; then
      current_start="$start"
      current_end="$end"
    fi
  done <<< "$rules"

  if [[ "$current_start" == "$address" && "$current_end" == "$address" ]]; then
    log_info "already admits $address alone"
  elif [[ -n "$current_start" ]]; then
    log_warn "moving the rule from $current_start to $current_end onto $address alone: the server at home reaches the database only when $address is the home's address"
    run az sql server firewall-rule update --resource-group "$ERP_AZURE_RESOURCE_GROUP" --server "$ERP_AZURE_SQL_SERVER_NAME" \
      --name "$SQL_FIREWALL_RULE_NAME" --start-ip-address "$address" --end-ip-address "$address" --output none --only-show-errors
    (( DRY_RUN )) || log_info "a changed rule takes up to five minutes to apply"
  else
    log_info "admitting $address"
    run az sql server firewall-rule create --resource-group "$ERP_AZURE_RESOURCE_GROUP" --server "$ERP_AZURE_SQL_SERVER_NAME" \
      --name "$SQL_FIREWALL_RULE_NAME" --start-ip-address "$address" --end-ip-address "$address" --output none --only-show-errors
    (( DRY_RUN )) || log_info "a new rule takes up to five minutes to apply"
  fi
}

run_address() {
  sql_server_exists || die "the server $ERP_AZURE_SQL_SERVER_NAME does not exist; run: bash scripts/azure/provision.sh"
  check_firewall_rules
  ensure_firewall_rule "$ALLOW_ADDRESS"
}

run_current_address() {
  sql_server_exists || die "the server $ERP_AZURE_SQL_SERVER_NAME does not exist; run: bash scripts/azure/provision.sh"
  check_firewall_rules
  resolve_sqlcmd
  resolve_sql_server_host
  log_step "This computer's address as Azure sees it"
  local result address home_address
  result="$(probe_sql_access)"
  home_address="$(home_rule_address)"
  case "$result" in
    answered*)
      address="${result#answered }"
      [[ -n "$address" ]] || die "the server answered without naming this computer's address"
      if [[ "$home_address" == "$address" ]]; then
        log_info "the rule $SQL_FIREWALL_RULE_NAME already admits this computer at $address alone"
        return 0
      fi
      log_info "the server answered this computer at $address through the range of the rule $SQL_FIREWALL_RULE_NAME"
      ensure_firewall_rule "$address"
      ;;
    refused*)
      log_info "refused from ${result#refused }"
      ensure_firewall_rule "${result#refused }"
      ;;
    signin-refused*)
      [[ -n "$home_address" ]] \
        || die "the server refused the sign-in after the range of the rule $SQL_FIREWALL_RULE_NAME admitted this computer, so this computer's address is unknown; run as the Microsoft Entra admin, or name the home's address with --allow-address <IPv4>"
      log_info "the server refused the sign-in, which happens only after its firewall has admitted this computer: the rule $SQL_FIREWALL_RULE_NAME admits $home_address alone"
      ;;
    *)
      die "cannot reach $SQL_SERVER_HOST: ${result#unreachable }"
      ;;
  esac
}

app_client_id() {
  local display_name="$1"
  local app_id
  app_id="$(service_principal_lookup "$display_name" appId)"
  [[ -n "$app_id" ]] || die "no service principal is named '$display_name'; run: bash scripts/azure/entra.sh"
  printf '%s' "$app_id"
}

print_users_plan() {
  printf '[dry-run] %s\n' \
    "create the users [$ERP_AZURE_APP_RUNTIME_NAME] and [$ERP_AZURE_APP_MIGRATOR_NAME] from Microsoft Entra ID" \
    "add [$ERP_AZURE_APP_MIGRATOR_NAME] to db_ddladmin, db_datareader and db_datawriter and grant it CREATE ROLE" \
    "once the migrations have created it, add [$ERP_AZURE_APP_RUNTIME_NAME] to [$SQL_AUDIT_APPEND_ONLY_ROLE], then to db_datareader and db_datawriter" >&2
}

require_entra_admin() {
  local operator_id="$1"
  local admin_id
  admin_id="$(az_read sql server ad-admin list --resource-group "$ERP_AZURE_RESOURCE_GROUP" --server "$ERP_AZURE_SQL_SERVER_NAME" \
    --query '[0].sid' --output tsv)" || die "cannot read the Microsoft Entra admin of $ERP_AZURE_SQL_SERVER_NAME"
  [[ "${admin_id,,}" == "${operator_id,,}" ]] \
    || die "the signed-in user $operator_id is not the Microsoft Entra admin of $ERP_AZURE_SQL_SERVER_NAME ($admin_id); only that admin can create its users"
}

report_users_output() {
  local output="$1"
  local line changes=0 pending=0
  while IFS= read -r line; do
    [[ -n "$line" ]] || continue
    case "$line" in
      change:*)
        changes=$((changes + 1))
        if (( DRY_RUN )); then
          printf '[dry-run] %s\n' "${line#change: }" >&2
        else
          log_info "${line#change: }"
        fi
        ;;
      pending:*)
        pending=1
        log_warn "${line#pending: }"
        ;;
      ok:*) log_info "${line#ok: }" ;;
      done) ;;
      *) log_info "$line" ;;
    esac
  done <<< "$output"

  log_step "Summary"
  if (( DRY_RUN )); then
    log_info "dry run: $changes change(s) would be made; nothing was changed"
  else
    log_info "$changes change(s) made"
  fi
  if (( pending )); then
    log_info "next steps:"
    log_info "  1. run the migrator once as its own identity (docs/operations/runbooks/database.md, section \"First migration\")"
    log_info "  2. bash scripts/azure/database.sh   (gives the runtime's user its roles, the append-only one first, and proves its rights)"
  else
    log_info "next step: bash scripts/azure/verify.sh --database"
  fi
}

run_users() {
  log_step "Resolving the operator and the service principals"
  local operator_id runtime_app_id migrator_app_id
  operator_id="$(signed_in_user_object_id)"
  log_info "operator: $operator_id"
  if (( DRY_RUN )); then
    runtime_app_id="$(service_principal_lookup "$ERP_AZURE_APP_RUNTIME_NAME" appId)"
    migrator_app_id="$(service_principal_lookup "$ERP_AZURE_APP_MIGRATOR_NAME" appId)"
    if [[ -z "$runtime_app_id" || -z "$migrator_app_id" ]]; then
      log_info "a service principal does not exist yet (entra.sh creates it), so the database's users cannot be read"
      print_users_plan
      return 0
    fi
  else
    runtime_app_id="$(app_client_id "$ERP_AZURE_APP_RUNTIME_NAME")"
    migrator_app_id="$(app_client_id "$ERP_AZURE_APP_MIGRATOR_NAME")"
  fi
  log_info "runtime '$ERP_AZURE_APP_RUNTIME_NAME': client id $runtime_app_id"
  log_info "migrator '$ERP_AZURE_APP_MIGRATOR_NAME': client id $migrator_app_id"

  log_step "Database $ERP_AZURE_SQL_DATABASE_NAME on $ERP_AZURE_SQL_SERVER_NAME"
  if ! sql_server_exists; then
    (( DRY_RUN )) || die "the server $ERP_AZURE_SQL_SERVER_NAME does not exist; run: bash scripts/azure/provision.sh"
    log_info "the server does not exist yet, so its state cannot be read"
    print_users_plan
    return 0
  fi
  require_entra_admin "$operator_id"
  check_firewall_rules
  resolve_sqlcmd
  resolve_sql_server_host
  if (( DRY_RUN )); then
    local result
    result="$(probe_sql_access)"
    case "$result" in
      answered*) ;;
      refused*)
        log_warn "the firewall refuses this computer's address ${result#refused }, so the database's state cannot be read; a real run needs, $SQL_HOME_HINT"
        print_users_plan
        return 0
        ;;
      *) die "cannot read the database: ${result#* }" ;;
    esac
  else
    wait_for_sql_access "$SQL_ACCESS_ATTEMPTS"
  fi

  local output
  if ! output="$(run_users_script "$DRY_RUN" "$runtime_app_id" "$migrator_app_id")"; then
    printf '%s\n' "$output" >&2
    die "the database users could not be converged (see the error above)"
  fi
  report_users_output "$output"
}

main() {
  parse_database_args "$@"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az
  [[ -f "$SQL_USERS_SCRIPT" ]] || die "$SQL_USERS_SCRIPT not found"

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  case "$MODE" in
    users) run_users ;;
    current-address) run_current_address ;;
    address) run_address ;;
    *) die "unknown mode '$MODE'" ;;
  esac
}

main "$@"
