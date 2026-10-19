#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/database.sh [--dry-run] [--params <file>]

Converges the users of the production database as its Microsoft Entra admin,
through the Go sqlcmd and the Azure CLI sign-in: the contained users of the
runtime and migrator service principals, each checked against its app'"'"'s
client id; db_ddladmin, the data roles and CREATE ROLE for the migrator; and,
once the migrations have created the schema audit and the role
erp_audit_append_only, the runtime'"'"'s membership of that role and its data
roles, proved under EXECUTE AS USER. Fails while the server holds a firewall
rule besides home, which scripts/azure/home-address.sh points at the home'"'"'s
public address. Safe to run repeatedly; never deletes a user.

Options:
  --dry-run         Print every change that would be made; change nothing.
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/sql.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/sql.sh"

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
        log_warn "the firewall refuses this computer's address ${result#refused }, so the database's state cannot be read; a real run needs, $HOME_ADDRESS_HINT"
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
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az
  [[ -f "$SQL_USERS_SCRIPT" ]] || die "$SQL_USERS_SCRIPT not found"

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  run_users
}

main "$@"
