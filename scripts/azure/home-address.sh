#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/home-address.sh [--address <IPv4>] [--dry-run] [--params <file>]

Points the two rules that admit the home'"'"'s public IPv4 address at it: the
firewall rule home of the production SQL server and the one IP rule of the
production storage account. Without --address it takes the address Azure sees
this computer connect from, so run it only from a computer on the home network,
without a VPN: the server at home reaches both only through that address. Fails,
before it changes anything, while the server holds another firewall rule or the
storage account admits more than one address (another rule, an approved private
endpoint, a network security perimeter). Safe to run repeatedly; it moves each
rule and never adds a second one.

Options:
  --address <IPv4>  Point both rules at this public IPv4 address instead.
  --dry-run         Print every change that would be made; change nothing.
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/sql.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/sql.sh"
# shellcheck source=lib/storage.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/storage.sh"

ADDRESS=''

parse_home_address_args() {
  local -a rest=()
  while (( $# > 0 )); do
    case "$1" in
      --address)
        [[ -n "${2:-}" ]] || die "--address requires an IPv4 address"
        ADDRESS="$2"
        shift
        ;;
      --address=*)
        ADDRESS="${1#--address=}"
        [[ -n "$ADDRESS" ]] || die "--address requires an IPv4 address"
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

current_address() {
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
        log_info "the database's rule $SQL_FIREWALL_RULE_NAME admits this computer at $address alone"
      else
        log_info "the server answered this computer at $address through the range of the rule $SQL_FIREWALL_RULE_NAME"
      fi
      ;;
    refused*)
      address="${result#refused }"
      log_info "refused from $address"
      ;;
    signin-refused*)
      [[ -n "$home_address" ]] \
        || die "the server refused the sign-in after the range of the rule $SQL_FIREWALL_RULE_NAME admitted this computer, so this computer's address is unknown; run as the Microsoft Entra admin, or name the home's address with --address <IPv4>"
      address="$home_address"
      log_info "the server refused the sign-in, which happens only after its firewall has admitted this computer: the rule $SQL_FIREWALL_RULE_NAME admits $address alone"
      ;;
    *)
      die "cannot reach $SQL_SERVER_HOST: ${result#unreachable }"
      ;;
  esac
  printf '%s' "$address"
}

main() {
  parse_home_address_args "$@"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az mktemp

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  log_step "Rules besides the home's address"
  sql_server_exists || die "the server $ERP_AZURE_SQL_SERVER_NAME does not exist; run: bash scripts/azure/provision.sh"
  production_storage_exists || die "the storage account $ERP_AZURE_STORAGE_PROD_NAME does not exist; run: bash scripts/azure/provision.sh"
  check_firewall_rules
  check_production_storage_network_rules
  log_info "none on $ERP_AZURE_SQL_SERVER_NAME or $ERP_AZURE_STORAGE_PROD_NAME"

  local address="$ADDRESS"
  if [[ -z "$address" ]]; then
    address="$(current_address)"
  fi
  is_public_ipv4 "$address" || die "'$address' is not a public IPv4 address; Azure sees the home only by its public address"
  ensure_firewall_rule "$address"
  ensure_production_storage_home_address "$address"

  log_step "Summary"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was changed"
  fi
  log_info "next step: bash scripts/azure/verify.sh --database, then bash scripts/azure/verify.sh"
}

main "$@"
