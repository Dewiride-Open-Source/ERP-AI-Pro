#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/check.sh

Syntax-checks every shell script under scripts/deploy and runs shellcheck when it is on PATH; checks that
the systemd units under infra/host/systemd run as the deploy account in the deployment checkout that
bootstrap.sh creates and wait for exactly the files it names, that the boot and nightly services run
compose under the lock deploy.sh and rollback.sh hold, that the boot unit never migrates, recreates or
pulls and tries a failed start again, that the nightly unit runs retain alone, that the heal unit runs
heal.sh of the deployment checkout every minute and starts no other unit, that the backup unit runs
backup.sh of the deployment checkout every night in India time, in a private runtime folder, once .env and
the backup'"'"'s two certificates exist, that infra/compose/compose.production.yaml runs every image by the
digest deploy.sh writes into .env, that infra/compose/compose.backup.yaml runs only Microsoft images by
digest, each container with a read-only root, no capability but the two its owner services need, no
privilege escalation, and no network but the Azure CLI'"'"'s, and that infra/host/docker/daemon.json is JSON;
runs the stand-in scenarios of deploy.sh and rollback.sh (tests/release-decisions.sh), of heal.sh
(tests/heal-decisions.sh) and of backup.sh and restore.sh (tests/backup-decisions.sh); and, where the
tools exist (an Ubuntu host or CI runner), verifies the units with systemd-analyze, the APT settings with
apt-config and the Docker daemon configuration with dockerd --validate. Needs no root and changes nothing.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/backup.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/backup.sh"

readonly UNIT_DIR='infra/host/systemd'
readonly APT_SETTINGS='infra/host/apt/52erp-ai-pro-unattended-upgrades.conf'
readonly DAEMON_CONFIG='infra/host/docker/daemon.json'
readonly BASE_COMPOSE='infra/compose/compose.yaml'
readonly PRODUCTION_COMPOSE='infra/compose/compose.production.yaml'
readonly BACKUP_COMPOSE="infra/compose/$BACKUP_COMPOSE_FILE"
readonly MICROSOFT_IMAGE_LINE='^    image: mcr\.microsoft\.com/[a-z0-9./-]+:[A-Za-z0-9._-]+@sha256:[0-9a-f]{64}$'
SHELL_SCRIPT_COUNT=0
declare -A SERVICE_BLOCKS=()
declare -A VOLUME_KEYS=()

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
  for certificate in "${CLIENT_CERTIFICATES[@]}" "$BACKUP_CLIENT_CERTIFICATE" "$BACKUP_RECIPIENT_CERTIFICATE"; do
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
        || die "$unit waits for ${condition#ConditionPathExists=}, which is neither .env nor a certificate of the stack or of the backup"
    done < <(grep '^ConditionPathExists=' "$unit")
    log_info "$unit"
  done
}

check_unit_commands() {
  log_step "the units start, sweep, heal and back up only as ADR-0049, ADR-0055 and ADR-0056 decide"
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
    [[ "$(sed -n 's/^ExecStart=//p' "$unit")" == "/usr/bin/flock $DEPLOY_LOCK /usr/bin/docker compose "* ]] \
      || die "$unit does not run compose under the lock $DEPLOY_LOCK that deploy.sh and rollback.sh hold"
  done
  grep -q '^OnCalendar=.* Asia/Kolkata$' "$timer" || die "$timer does not keep its time in India time"
  local heal="$UNIT_DIR/erp-ai-pro-heal.service" heal_timer="$UNIT_DIR/erp-ai-pro-heal.timer"
  [[ "$(sed -n 's/^ExecStart=//p' "$heal")" == "/usr/bin/bash $DEPLOY_CHECKOUT/scripts/deploy/heal.sh" ]] \
    || die "$heal does not run scripts/deploy/heal.sh of the deployment checkout"
  if grep -qE '^(Wants|Requires)=' "$heal"; then
    die "$heal starts another unit, but it only looks at the running stack"
  fi
  grep -qx 'OnCalendar=minutely' "$heal_timer" || die "$heal_timer does not run every minute"
  check_backup_unit
  log_info "the boot never migrates, recreates or pulls, retries a failed start, and the nightly run is retain alone"
  log_info "both wait for the lock $DEPLOY_LOCK while a deployment or rollback holds it"
  log_info "the heal unit runs heal.sh every minute, which takes the same lock without waiting"
  log_info "the backup unit runs backup.sh every night in a private runtime folder once .env and the backup's two certificates exist"
}

check_backup_unit() {
  local backup="$UNIT_DIR/erp-ai-pro-backup.service" timer="$UNIT_DIR/erp-ai-pro-backup.timer" setting file
  [[ "$(sed -n 's/^ExecStart=//p' "$backup")" == "/usr/bin/bash $DEPLOY_CHECKOUT/scripts/deploy/backup.sh" ]] \
    || die "$backup does not run scripts/deploy/backup.sh of the deployment checkout"
  grep -qx 'Wants=docker.service network-online.target' "$backup" || die "$backup does not want docker.service and the network"
  if grep -q '^Requires=' "$backup"; then
    die "$backup requires a unit, so restarting that unit would stop it"
  fi
  for setting in RuntimeDirectory=erp-ai-pro-backup RuntimeDirectoryMode=0700 UMask=0077; do
    grep -qxF "$setting" "$backup" || die "$backup does not set $setting, which keeps the plaintext copy private and in memory"
  done
  for file in .env "secrets/$BACKUP_CLIENT_CERTIFICATE" "secrets/$BACKUP_RECIPIENT_CERTIFICATE"; do
    grep -qxF "ConditionPathExists=$DEPLOY_COMPOSE_DIR/$file" "$backup" || die "$backup does not wait for $DEPLOY_COMPOSE_DIR/$file"
  done
  (( $(grep -c '^ConditionPathExists=' "$backup") == 3 )) || die "$backup waits for other files than .env and the backup's two certificates"
  grep -qxF "readonly BACKUP_FILES=($BACKUP_CLIENT_CERTIFICATE $BACKUP_RECIPIENT_CERTIFICATE)" scripts/deploy/bootstrap.sh \
    || die "scripts/deploy/bootstrap.sh does not name the backup's certificates $BACKUP_CLIENT_CERTIFICATE and $BACKUP_RECIPIENT_CERTIFICATE"
  grep -q '^OnCalendar=.* Asia/Kolkata$' "$timer" || die "$timer does not keep its time in India time"
  grep -qx 'Persistent=true' "$timer" || die "$timer does not catch up a night the server was off"
}

read_service_blocks() {
  local file="$1" line service='' in_services=0 name='^  ([a-z][a-z0-9-]*):$'
  SERVICE_BLOCKS=()
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ "$line" =~ ^[^[:space:]] ]]; then
      in_services=0
      service=''
      if [[ "$line" == 'services:' ]]; then
        in_services=1
      fi
      continue
    fi
    if (( ! in_services )); then
      continue
    fi
    if [[ "$line" =~ $name ]]; then
      service="${BASH_REMATCH[1]}"
      SERVICE_BLOCKS[$service]=''
    elif [[ -n "$service" ]]; then
      SERVICE_BLOCKS[$service]+="$line"$'\n'
    fi
  done < "$file"
}

service_has() {
  grep -qxF -- "$2" <<< "${SERVICE_BLOCKS[$1]}"
}

read_volume_keys() {
  local file="$1" line key='' in_volumes=0 entry='^  ([a-z][a-z0-9-]*):$' named='^    name: (.+)$'
  VOLUME_KEYS=()
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ "$line" =~ ^[^[:space:]] ]]; then
      in_volumes=0
      key=''
      if [[ "$line" == 'volumes:' ]]; then
        in_volumes=1
      fi
      continue
    fi
    if (( ! in_volumes )); then
      continue
    fi
    if [[ "$line" =~ $entry ]]; then
      key="${BASH_REMATCH[1]}"
    elif [[ -n "$key" && "$line" =~ $named ]]; then
      VOLUME_KEYS[${BASH_REMATCH[1]}]="$key"
    fi
  done < "$file"
}

service_mounts() {
  awk '/^    volumes:$/ { inside = 1; next } inside && /^    [a-z_]+:/ { inside = 0 } inside' <<< "${SERVICE_BLOCKS[$1]}"
}

check_backup_compose() {
  log_step "$BACKUP_COMPOSE runs Microsoft images by digest, unprivileged, read-only, without capabilities or a container log"
  local service index role user image setting volume expected_count mounts
  local -a expected=(azure-cli)
  read_service_blocks "$BACKUP_COMPOSE"
  read_volume_keys "$BACKUP_COMPOSE"
  for index in "${!BACKUP_VOLUME_SERVICES[@]}"; do
    for role in reader writer owner; do
      expected+=("${BACKUP_VOLUME_SERVICES[$index]}-$role")
    done
  done
  expected_count="${#expected[@]}"
  (( ${#SERVICE_BLOCKS[@]} == expected_count )) \
    || die "$BACKUP_COMPOSE defines ${#SERVICE_BLOCKS[@]} service(s), not the $expected_count: ${expected[*]}"
  for service in "${expected[@]}"; do
    [[ -n "${SERVICE_BLOCKS[$service]+set}" ]] || die "$BACKUP_COMPOSE has no service $service"
    image="$(grep -E '^    image:' <<< "${SERVICE_BLOCKS[$service]}" || true)"
    [[ "$image" =~ $MICROSOFT_IMAGE_LINE ]] || die "the $service service of $BACKUP_COMPOSE does not run a Microsoft image by its digest: ${image:-no image}"
    for setting in '    pull_policy: missing' '    read_only: true' '    cap_drop: [ALL]' '      - no-new-privileges:true' '    logging:' '      driver: none'; do
      service_has "$service" "$setting" || die "the $service service of $BACKUP_COMPOSE lacks '${setting#"${setting%%[![:space:]]*}"}'"
    done
    if grep -qE '^    (privileged|ports|pid|ipc|devices|volumes_from):' <<< "${SERVICE_BLOCKS[$service]}"; then
      die "the $service service of $BACKUP_COMPOSE asks for more than a one-shot helper needs"
    fi
  done
  for index in "${!BACKUP_VOLUME_SERVICES[@]}"; do
    user="${BACKUP_VOLUME_OWNERS[$index]}"
    for role in reader writer owner; do
      service="${BACKUP_VOLUME_SERVICES[$index]}-$role"
      service_has "$service" '    network_mode: none' || die "the $service service of $BACKUP_COMPOSE has a network"
      mounts="$(service_mounts "$service")"
      (( $(grep -c '^      - ' <<< "$mounts") == 1 )) && grep -qxF '      - type: volume' <<< "$mounts" \
        || die "the $service service of $BACKUP_COMPOSE mounts other than its one volume"
      grep -qxF "        source: ${VOLUME_KEYS[${BACKUP_VOLUMES[$index]}]:-}" <<< "$mounts" \
        || die "the $service service of $BACKUP_COMPOSE does not mount ${BACKUP_VOLUMES[$index]}"
      grep -qxF "        target: /volume" <<< "$mounts" || die "the $service service of $BACKUP_COMPOSE does not mount its volume at /volume"
      if [[ "$role" == owner ]]; then
        service_has "$service" '    user: "0:0"' || die "the $service service of $BACKUP_COMPOSE does not run as root, which alone may give the volume to $user"
        service_has "$service" '    cap_add: [CHOWN, FOWNER]' || die "the $service service of $BACKUP_COMPOSE adds other capabilities than CHOWN and FOWNER"
      else
        service_has "$service" "    user: \"$user\"" || die "the $service service of $BACKUP_COMPOSE does not run as the volume's owner $user"
        if grep -q '^    cap_add:' <<< "${SERVICE_BLOCKS[$service]}"; then
          die "the $service service of $BACKUP_COMPOSE adds a capability"
        fi
      fi
      if [[ "$role" == reader ]] && ! grep -qxF '        read_only: true' <<< "$mounts"; then
        die "the $service service of $BACKUP_COMPOSE mounts its volume writable"
      fi
      if [[ "$role" != reader ]] && grep -qxF '        read_only: true' <<< "$mounts"; then
        die "the $service service of $BACKUP_COMPOSE mounts its volume read-only, so it cannot fill it"
      fi
    done
  done
  service_has azure-cli '    network_mode: bridge' || die "the azure-cli service of $BACKUP_COMPOSE does not use Docker's default bridge"
  service_has azure-cli '    user: "1654:1654"' || die "the azure-cli service of $BACKUP_COMPOSE does not run as 1654, the owner of $BACKUP_CLIENT_CERTIFICATE"
  service_has azure-cli "      - $BACKUP_CLIENT_CERTIFICATE" || die "the azure-cli service of $BACKUP_COMPOSE does not mount $BACKUP_CLIENT_CERTIFICATE"
  if grep -q '^    cap_add:' <<< "${SERVICE_BLOCKS[azure-cli]}"; then
    die "the azure-cli service of $BACKUP_COMPOSE adds a capability"
  fi
  if grep -q '^    volumes:' <<< "${SERVICE_BLOCKS[azure-cli]}"; then
    die "the azure-cli service of $BACKUP_COMPOSE mounts a volume; backup.sh mounts the sealed archive alone, read-only, for an upload"
  fi
  for setting in '--username "$$BACKUP_CLIENT_ID"' '--tenant "$$BACKUP_TENANT_ID"' "--certificate /run/secrets/$BACKUP_CLIENT_CERTIFICATE" 'exec az "$$@"'; do
    grep -qF -- "$setting" <<< "${SERVICE_BLOCKS[azure-cli]}" \
      || die "the azure-cli service of $BACKUP_COMPOSE does not sign in with $setting, which lib/backup.sh passes"
  done
  grep -qxF "    file: ./secrets/$BACKUP_CLIENT_CERTIFICATE" "$BACKUP_COMPOSE" || die "$BACKUP_COMPOSE does not read $BACKUP_CLIENT_CERTIFICATE from ./secrets"
  for volume in "${BACKUP_VOLUMES[@]}"; do
    grep -qxF "    name: $volume" "$BACKUP_COMPOSE" || die "$BACKUP_COMPOSE does not name the external volume $volume"
    is_one_of "$volume" "${STACK_VOLUMES[@]}" || die "$volume is not a volume the bootstrap creates (${STACK_VOLUMES[*]})"
  done
  (( $(grep -c '^    external: true$' "$BACKUP_COMPOSE") == ${#BACKUP_VOLUMES[@]} )) \
    || die "$BACKUP_COMPOSE declares other volumes than the stack's two external ones"
  log_info "all $expected_count services run a Microsoft image by digest and keep no container log; each volume is mounted by its own three helpers alone, read by its owner read-only, filled by its owner and given to its owner by root with CHOWN and FOWNER alone, without a network"
}

check_backup_period() {
  log_step "the backups' write-once period is the same everywhere"
  grep -qxF "param backupImmutabilityPeriodDays int = $BACKUP_IMMUTABILITY_DAYS" scripts/azure/bicep/main.bicep \
    || die "scripts/azure/bicep/main.bicep does not default backupImmutabilityPeriodDays to the $BACKUP_IMMUTABILITY_DAYS days by which backup.sh deletes archives"
  grep -qxF "readonly BACKUP_IMMUTABILITY_DAYS='$BACKUP_IMMUTABILITY_DAYS'" scripts/azure/verify.sh \
    || die "scripts/azure/verify.sh does not check the $BACKUP_IMMUTABILITY_DAYS days by which backup.sh deletes archives"
  log_info "main.bicep, verify.sh and backup.sh agree on $BACKUP_IMMUTABILITY_DAYS days"
}

check_release_pins() {
  log_step "$PRODUCTION_COMPOSE runs the images by the digests deploy.sh writes"
  local image line setting base_count production_count
  local -a pinned=()
  for image in "${RELEASE_IMAGES[@]}"; do
    line="image: $RELEASE_REGISTRY/$image:\${IMAGE_TAG:?set by deploy.sh}@\${${image^^}_IMAGE_DIGEST:?set by deploy.sh}"
    grep -qF -- "$line" "$PRODUCTION_COMPOSE" || die "$PRODUCTION_COMPOSE does not run the $image image as '$line'"
    pinned+=("$line")
  done
  while IFS= read -r line; do
    setting="${line#"${line%%[![:space:]]*}"}"
    is_one_of "$setting" "${pinned[@]}" || die "$PRODUCTION_COMPOSE names an image without the digest deploy.sh writes: $setting"
  done < <(grep -E '^[[:space:]]+image:' "$PRODUCTION_COMPOSE")
  base_count="$(grep -cE '^[[:space:]]+image:' "$BASE_COMPOSE")"
  production_count="$(grep -cE '^[[:space:]]+image:' "$PRODUCTION_COMPOSE")"
  (( base_count == production_count )) \
    || die "$BASE_COMPOSE names $base_count image(s) and $PRODUCTION_COMPOSE pins $production_count, so a service would run an unpinned tag"
  log_info "all $production_count services run their image by the digest deploy.sh writes"
}

check_release_decisions() {
  log_step "deploy.sh and rollback.sh against stand-ins (tests/release-decisions.sh)"
  bash scripts/deploy/tests/release-decisions.sh
}

check_heal_decisions() {
  log_step "heal.sh against stand-ins (tests/heal-decisions.sh)"
  bash scripts/deploy/tests/heal-decisions.sh
}

check_backup_decisions() {
  log_step "backup.sh and restore.sh against stand-ins and the real OpenSSL (tests/backup-decisions.sh)"
  bash scripts/deploy/tests/backup-decisions.sh
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
  check_release_pins
  check_backup_compose
  check_backup_period
  check_daemon_config
  check_units_with_systemd
  check_apt_settings
  check_release_decisions
  check_heal_decisions
  check_backup_decisions
  printf 'deploy scripts check ok: %s shell script(s) parse, the host configuration is consistent and the release, heal and backup decisions hold\n' "$SHELL_SCRIPT_COUNT"
}

main "$@"
