#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/check.sh

Syntax-checks every shell script under scripts/deploy and runs shellcheck when it is on PATH; checks that
the systemd units under infra/host/systemd run as the deploy account in the deployment checkout that
bootstrap.sh creates and wait for exactly the files it names, that the boot unit never migrates,
recreates or pulls and tries a failed start again, that the nightly unit runs retain alone, and that
infra/host/docker/daemon.json is JSON; and, where the tools exist (an Ubuntu host or CI runner),
verifies the units with systemd-analyze,
the APT settings with apt-config and the Docker daemon configuration with dockerd --validate. Needs no
root and changes nothing.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"

readonly UNIT_DIR='infra/host/systemd'
readonly APT_SETTINGS='infra/host/apt/52erp-ai-pro-unattended-upgrades.conf'
readonly DAEMON_CONFIG='infra/host/docker/daemon.json'
SHELL_SCRIPT_COUNT=0

check_shell_scripts() {
  log_step "bash -n"
  local -a scripts=()
  mapfile -t scripts < <(find scripts/deploy -type f -name '*.sh' | sort)
  (( ${#scripts[@]} > 0 )) || die "no shell scripts found under scripts/deploy"
  local script
  for script in "${scripts[@]}"; do
    bash -n "$script"
    log_info "$script"
  done
  if command -v shellcheck > /dev/null 2>&1; then
    log_step "shellcheck"
    local -a entry_points=()
    for script in "${scripts[@]}"; do
      if [[ "$script" != scripts/deploy/lib/* ]]; then
        entry_points+=("$script")
      fi
    done
    shellcheck --external-sources --source-path=SCRIPTDIR --shell=bash --severity=warning "${entry_points[@]}"
    log_info "clean, with the libraries checked through the scripts that source them"
  else
    log_info "shellcheck not on PATH; skipped"
  fi
  SHELL_SCRIPT_COUNT="${#scripts[@]}"
}

is_stack_file() {
  local path="$1" certificate
  if [[ "$path" == "$DEPLOY_COMPOSE_DIR/.env" ]]; then
    return 0
  fi
  for certificate in "${CLIENT_CERTIFICATES[@]}"; do
    if [[ "$path" == "$DEPLOY_COMPOSE_DIR/secrets/$certificate" ]]; then
      return 0
    fi
  done
  return 1
}

check_unit_paths() {
  log_step "the units match the deploy account and checkout"
  local unit condition
  for unit in "$UNIT_DIR"/*.service; do
    grep -qxF "User=$DEPLOY_USER" "$unit" || die "$unit does not run as User=$DEPLOY_USER"
    grep -qxF "Group=$DEPLOY_USER" "$unit" || die "$unit does not run as Group=$DEPLOY_USER"
    grep -qxF "WorkingDirectory=$DEPLOY_COMPOSE_DIR" "$unit" || die "$unit does not run in WorkingDirectory=$DEPLOY_COMPOSE_DIR"
    grep -qxF "ConditionPathExists=$DEPLOY_COMPOSE_DIR/.env" "$unit" || die "$unit does not wait for $DEPLOY_COMPOSE_DIR/.env"
    while IFS= read -r condition; do
      is_stack_file "${condition#ConditionPathExists=}" \
        || die "$unit waits for ${condition#ConditionPathExists=}, which is neither .env nor a client certificate of the stack"
    done < <(grep '^ConditionPathExists=' "$unit")
    log_info "$unit"
  done
}

check_unit_commands() {
  log_step "the units start and sweep only as ADR-0049 decides"
  local boot="$UNIT_DIR/erp-ai-pro.service" retain="$UNIT_DIR/erp-ai-pro-retain.service" timer="$UNIT_DIR/erp-ai-pro-retain.timer"
  local start flag unit
  start="$(sed -n 's/^ExecStart=//p' "$boot")"
  for flag in ' up ' ' --detach' ' --wait' ' --no-build' ' --no-recreate' ' --pull never' ' --no-deps'; do
    [[ "$start" == *"$flag"* ]] || die "$boot does not start the stack with '${flag# }'"
  done
  [[ "$start" == *' api web proxy' ]] || die "$boot does not start exactly the api, web and proxy services"
  if [[ "$start" == *migrator* ]]; then
    die "$boot names the migrator, which only a deployment runs"
  fi
  [[ "$(sed -n 's/^ExecStop=//p' "$boot")" == *' stop' ]] || die "$boot does not stop the stack at shutdown"
  grep -qx 'Restart=on-failure' "$boot" || die "$boot does not try again after a failed start"
  [[ "$(sed -n 's/^ExecStart=//p' "$retain")" == *' run --rm -T migrator retain' ]] || die "$retain does not run the migrator's retain command alone"
  for unit in "$boot" "$retain"; do
    grep -qx 'Wants=docker.service network-online.target' "$unit" || die "$unit does not want docker.service"
    if grep -q '^Requires=' "$unit"; then
      die "$unit requires a unit, so restarting that unit would stop it"
    fi
  done
  grep -q '^OnCalendar=.* Asia/Kolkata$' "$timer" || die "$timer does not keep its time in India time"
  log_info "the boot never migrates, recreates or pulls, retries a failed start, and the nightly run is retain alone"
}

check_daemon_config() {
  log_step "$DAEMON_CONFIG"
  require_command node
  node -e 'JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8"));' "$DAEMON_CONFIG"
  log_info "JSON"
  if command -v dockerd > /dev/null 2>&1; then
    dockerd --validate --config-file "$DAEMON_CONFIG"
  else
    log_info "dockerd not on PATH; skipped"
  fi
}

check_units_with_systemd() {
  if ! command -v systemd-analyze > /dev/null 2>&1 || [[ ! -d /run/systemd/system ]]; then
    log_info "systemd-analyze not available; the units were not verified"
    return 0
  fi
  log_step "systemd-analyze"
  systemd-analyze verify "$UNIT_DIR"/*.service "$UNIT_DIR"/*.timer
  local calendar
  while IFS= read -r calendar; do
    systemd-analyze calendar "${calendar#OnCalendar=}" > /dev/null
    log_info "$calendar"
  done < <(grep -h '^OnCalendar=' "$UNIT_DIR"/*.timer)
  log_info "the units verify"
}

check_apt_settings() {
  if ! command -v apt-config > /dev/null 2>&1; then
    log_info "apt-config not available; $APT_SETTINGS was not read"
    return 0
  fi
  log_step "apt-config"
  local effective setting
  effective="$(apt-config -c "$APT_SETTINGS" dump)"
  while IFS= read -r setting; do
    grep -qxF -- "$setting" <<< "$effective" || die "apt-config does not read '$setting' from $APT_SETTINGS"
    log_info "$setting"
  done < <(grep -vE '^[[:space:]]*(//|#|$)' "$APT_SETTINGS")
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command bash find sort grep
  check_shell_scripts
  check_unit_paths
  check_unit_commands
  check_daemon_config
  check_units_with_systemd
  check_apt_settings
  printf 'deploy scripts check ok: %s shell script(s) parse and the host configuration is consistent\n' "$SHELL_SCRIPT_COUNT"
}

main "$@"
