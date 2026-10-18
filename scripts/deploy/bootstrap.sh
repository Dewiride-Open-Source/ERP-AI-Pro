#!/usr/bin/env bash

USAGE='Usage: sudo bash scripts/deploy/bootstrap.sh --site-address <IPv4> --authorized-keys <file> [--home-network <CIDR>] [--dry-run]

Prepares an Ubuntu 22.04, 24.04 or 26.04 LTS server for the ERP-AI-Pro production stack and keeps it so
when run again: Docker Engine with live restore, the deploy account erp-ai-pro as the only member of the
docker group and nobody in the lxd group, its checkout of this repository in /srv/erp-ai-pro/ERP-AI-Pro
with the folder of the client certificates, .env and the two external volumes, the systemd units that
start the stack at boot and run the retention sweeps every night, security updates that restart the
server at 03:00 India time when one needs it, SSH with public keys only, and a firewall that admits SSH
from the home network only. Every step changes only what differs, so a second run changes nothing.
Runbook: docs/operations/runbooks/host-setup.md

Options:
  --site-address <IPv4>      The fixed address of this server on the home network, which people open the ERP at.
  --authorized-keys <file>   The deploy account'"'"'s own public keys, in authorized_keys format; none may also sign in
                             as the administrator.
  --home-network <CIDR>      The network allowed to reach SSH (default: the network of --site-address).
  --dry-run                  List what a run would change and change nothing; without root it compares what it can read.
  --help                     Show this help.'

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"

readonly SUPPORTED_RELEASES=(jammy noble resolute)
readonly BASE_PACKAGES=(openssh-server ufw unattended-upgrades git ca-certificates curl python3)
readonly APT_OPTIONS=(-qq -o DPkg::Lock::Timeout=600)
readonly DOCKER_REPOSITORY='https://download.docker.com/linux/ubuntu'
readonly DOCKER_KEYRING='/etc/apt/keyrings/docker.asc'
readonly DOCKER_SOURCE='/etc/apt/sources.list.d/docker.sources'
readonly DOCKER_PACKAGES=(docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin)
readonly DOCKER_CONFLICTS=(docker.io docker-compose docker-compose-v2 docker-doc docker-buildx podman-docker containerd runc)
readonly DAEMON_CONFIG='/etc/docker/daemon.json'
readonly LXD_INSTALLER_SOCKET='lxd-installer.socket'
readonly SSH_RULES_FILE='/etc/ssh/sshd_config.d/10-erp-ai-pro.conf'
readonly SSH_EFFECTIVE_SETTINGS=(
  'passwordauthentication no'
  'kbdinteractiveauthentication no'
  'authenticationmethods publickey'
  'permitrootlogin no'
  'pubkeyauthentication yes'
  'x11forwarding no'
)
readonly UPDATES_FILE='/etc/apt/apt.conf.d/52erp-ai-pro-unattended-upgrades.conf'
readonly UPDATE_SERVICES=(apt-daily.service apt-daily-upgrade.service)
readonly UPDATE_TIMERS=(apt-daily.timer apt-daily-upgrade.timer)
readonly UNIT_FILES=(erp-ai-pro.service erp-ai-pro-retain.service erp-ai-pro-retain.timer)
readonly ENABLED_UNITS=(erp-ai-pro.service erp-ai-pro-retain.timer)
readonly RETAIN_TIMER='erp-ai-pro-retain.timer'
readonly SSH_RULE_COMMENT='SSH from the home network'

SITE_ADDRESS=''
AUTHORIZED_KEYS_FILE=''
HOME_NETWORK=''
ADMIN_USER=''

set_option() {
  [[ -n "$2" ]] || die "$1 requires a value"
  case "$1" in
    --site-address) SITE_ADDRESS="$2" ;;
    --authorized-keys) AUTHORIZED_KEYS_FILE="$2" ;;
    --home-network) HOME_NETWORK="$2" ;;
  esac
}

parse_bootstrap_args() {
  local -a rest=()
  while (( $# > 0 )); do
    case "$1" in
      --site-address | --authorized-keys | --home-network)
        set_option "$1" "${2:-}"
        shift
        ;;
      --site-address=* | --authorized-keys=* | --home-network=*)
        set_option "${1%%=*}" "${1#*=}"
        ;;
      *)
        rest+=("$1")
        ;;
    esac
    shift
  done
  parse_args "${rest[@]}"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  [[ -n "$SITE_ADDRESS" ]] || die "--site-address is required (see --help)"
  [[ -n "$AUTHORIZED_KEYS_FILE" ]] || die "--authorized-keys is required (see --help)"
}

os_release_field() {
  awk -v key="$1" 'index($0, key "=") == 1 && !found { value = substr($0, length(key) + 2); gsub(/"/, "", value); print value; found = 1 }' /etc/os-release
}

is_supported_release() {
  local release
  for release in "${SUPPORTED_RELEASES[@]}"; do
    if [[ "$1" == "$release" ]]; then
      return 0
    fi
  done
  return 1
}

package_installed() {
  [[ "$(dpkg-query -W -f='${Status}' "$1" 2> /dev/null || true)" == 'install ok installed' ]]
}

unit_state() {
  systemctl is-enabled "$1" 2> /dev/null || true
}

# ufw translates what it prints, so every call whose output is read runs in the C locale.
ufw_c() {
  LC_ALL=C ufw "$@"
}

apt_update() {
  if (( DRY_RUN )); then
    printf '[dry-run] apt-get update\n' >&2
    return 0
  fi
  local attempt
  for attempt in 1 2 3 4 5 6 7 8 9 10; do
    if apt-get "${APT_OPTIONS[@]}" update; then
      return 0
    fi
    log_warn "apt-get update failed (attempt $attempt of 10), usually because the daily update holds the package lists; trying again in 30 seconds"
    sleep 30
  done
  die "apt-get update kept failing; see the errors above"
}

check_platform() {
  log_step "Platform"
  [[ -r /etc/os-release ]] || die "/etc/os-release is missing; this script supports Ubuntu LTS only"
  local name
  name="$(os_release_field PRETTY_NAME)"
  [[ "$(os_release_field ID)" == ubuntu ]] || die "this script supports Ubuntu LTS only, not $name"
  is_supported_release "$(os_release_field VERSION_CODENAME)" || die "$name is not supported; use Ubuntu 22.04, 24.04 or 26.04 LTS"
  [[ -d /run/systemd/system ]] || die "systemd is not running; this script prepares a server, not a container"
  local zone
  zone="$(timedatectl show --property=Timezone --value 2> /dev/null || true)"
  [[ "$zone" == Etc/UTC || "$zone" == UTC ]] \
    || die "the clock is set to '${zone:-unknown}', and the nightly restart time assumes UTC; set it back first: sudo timedatectl set-timezone Etc/UTC"
  log_info "$name, clock in UTC"

  if is_root; then
    ADMIN_USER="${SUDO_USER:-}"
    [[ -n "$ADMIN_USER" && "$ADMIN_USER" != root ]] \
      || die "run this script with sudo from the administrator's own account, so it can check that the account keeps a way to sign in"
  elif (( DRY_RUN )); then
    ADMIN_USER="$(id -un)"
    log_info "dry run without root: what only root can read is listed as a change, which a run with sudo compares"
  else
    die "run this script with sudo, or add --dry-run to see what it would change"
  fi
}

admin_keys_file() {
  printf '%s/.ssh/authorized_keys' "$(getent passwd "$ADMIN_USER" | cut -d: -f6)"
}

check_arguments() {
  log_step "Arguments"
  is_ipv4 "$SITE_ADDRESS" || die "--site-address must be an IPv4 address such as 192.168.1.20, not '$SITE_ADDRESS'"
  local assigned
  assigned="$(ip -4 -o address show | awk -v address="$SITE_ADDRESS" '{ split($4, parts, "/") } parts[1] == address && !found { print $2, parts[2]; found = 1 }')"
  [[ -n "$assigned" ]] || die "$SITE_ADDRESS is not an address of this server; 'ip -4 address' lists them"
  local interface="${assigned% *}" prefix="${assigned#* }"
  if [[ -z "$HOME_NETWORK" ]]; then
    HOME_NETWORK="$(ipv4_network_of "$SITE_ADDRESS" "$prefix")"
  fi
  is_ipv4_network "$HOME_NETWORK" || die "--home-network must be an IPv4 network such as 192.168.1.0/24, not '$HOME_NETWORK'"
  ipv4_in_network "$SITE_ADDRESS" "$HOME_NETWORK" || die "$SITE_ADDRESS is not in the home network $HOME_NETWORK"
  log_info "site address $SITE_ADDRESS on $interface, home network $HOME_NETWORK"

  [[ -f "$AUTHORIZED_KEYS_FILE" && -r "$AUTHORIZED_KEYS_FILE" ]] || die "--authorized-keys: cannot read '$AUTHORIZED_KEYS_FILE'"
  local count
  count="$(valid_key_count "$AUTHORIZED_KEYS_FILE")" || die "--authorized-keys: '$AUTHORIZED_KEYS_FILE' holds a line that is not a public key"
  (( count > 0 )) || die "--authorized-keys: '$AUTHORIZED_KEYS_FILE' holds no public key"

  local admin_keys shared
  admin_keys="$(admin_keys_file)"
  if [[ -r "$admin_keys" ]] && valid_key_count "$admin_keys" > /dev/null; then
    shared="$(comm -12 <(key_fingerprints "$AUTHORIZED_KEYS_FILE" | sort -u) <(key_fingerprints "$admin_keys" | sort -u))"
    [[ -z "$shared" ]] \
      || die "--authorized-keys shares $shared with $admin_keys; the deploy account is root through Docker, so it needs a key of its own (docs/operations/runbooks/host-setup.md)"
  fi
  log_info "$count key(s) of its own for $DEPLOY_USER from $AUTHORIZED_KEYS_FILE"
}

ensure_packages() {
  log_step "Packages"
  local -a missing=()
  local package
  for package in "${BASE_PACKAGES[@]}"; do
    if ! package_installed "$package"; then
      missing+=("$package")
    fi
  done
  if (( ${#missing[@]} == 0 )); then
    log_info "in place: ${BASE_PACKAGES[*]}"
    return 0
  fi
  record_change "install ${missing[*]}"
  apt_update
  run env DEBIAN_FRONTEND=noninteractive apt-get "${APT_OPTIONS[@]}" install -y --no-install-recommends "${missing[@]}"
}

docker_repository_configured() {
  grep -rqsF -- "$DOCKER_REPOSITORY" /etc/apt/sources.list /etc/apt/sources.list.d/
}

add_docker_repository() {
  local codename architecture
  codename="$(os_release_field UBUNTU_CODENAME)"
  [[ -n "$codename" ]] || codename="$(os_release_field VERSION_CODENAME)"
  architecture="$(dpkg --print-architecture)"
  run install -m 0755 -d /etc/apt/keyrings
  run curl -fsSL --retry 3 "$DOCKER_REPOSITORY/gpg" -o "$DOCKER_KEYRING"
  run chmod a+r "$DOCKER_KEYRING"
  if (( DRY_RUN )); then
    printf '[dry-run] write %s for %s on %s\n' "$DOCKER_SOURCE" "$codename" "$architecture" >&2
  else
    printf 'Types: deb\nURIs: %s\nSuites: %s\nComponents: stable\nArchitectures: %s\nSigned-By: %s\n' \
      "$DOCKER_REPOSITORY" "$codename" "$architecture" "$DOCKER_KEYRING" > "$DOCKER_SOURCE"
  fi
  apt_update
}

ensure_docker_engine() {
  log_step "Docker Engine"
  if command -v docker > /dev/null 2>&1 && docker compose version > /dev/null 2>&1; then
    if ! package_installed docker-ce; then
      log_warn "Docker Engine comes from another source than Docker's repository; this script keeps it, and the runbook names the packages it expects"
    elif docker_repository_configured; then
      log_info "in place: Docker Engine $(dpkg-query -W -f='${Version}' docker-ce) from Docker's repository, Compose $(docker compose version --short)"
    else
      record_change "add Docker's apt repository, without which apt cannot update the installed Docker Engine"
      add_docker_repository
    fi
  else
    local -a conflicts=()
    local package
    for package in "${DOCKER_CONFLICTS[@]}"; do
      if package_installed "$package"; then
        conflicts+=("$package")
      fi
    done
    (( ${#conflicts[@]} == 0 )) || die "remove the unofficial Docker packages first, as Docker's install guide asks: sudo apt-get remove ${conflicts[*]}"
    record_change "install Docker Engine from Docker's repository: ${DOCKER_PACKAGES[*]}"
    add_docker_repository
    run env DEBIAN_FRONTEND=noninteractive apt-get "${APT_OPTIONS[@]}" install -y "${DOCKER_PACKAGES[@]}"
  fi
  local service state
  for service in docker.service containerd.service; do
    state="$(unit_state "$service")"
    if [[ "$service" == containerd.service && ( -z "$state" || "$state" == not-found ) ]]; then
      continue
    fi
    if [[ "$state" != enabled ]]; then
      record_change "start $service at boot"
      run systemctl enable --now --quiet "$service"
    fi
  done
}

daemon_config_covers() {
  python3 - "$1" "$2" << 'PYTHON'
import json, sys
with open(sys.argv[1], encoding="utf-8") as required_file, open(sys.argv[2], encoding="utf-8") as present_file:
    required, present = json.load(required_file), json.load(present_file)
sys.exit(0 if isinstance(present, dict) and all(present.get(key) == value for key, value in required.items()) else 1)
PYTHON
}

live_restore_enabled() {
  [[ "$(docker info --format '{{.LiveRestoreEnabled}}' 2> /dev/null || true)" == true ]]
}

ensure_docker_daemon_config() {
  log_step "Docker daemon configuration"
  local source="$HOST_CONFIG_DIR/docker/daemon.json" apply=0
  if [[ -f "$DAEMON_CONFIG" ]]; then
    [[ -r "$DAEMON_CONFIG" ]] || die "cannot read $DAEMON_CONFIG"
    daemon_config_covers "$source" "$DAEMON_CONFIG" \
      || die "$DAEMON_CONFIG lacks live restore or log rotation; add these settings to it, keeping its own, and run this script again: $(tr -s '\n ' ' ' < "$source")"
    log_info "in place: live restore and log rotation in $DAEMON_CONFIG"
  else
    if install_config "$source" "$DAEMON_CONFIG" 644 root:root "Docker daemon configuration: live restore and log rotation"; then
      apply=1
    fi
  fi
  if (( ! apply )) && is_root && command -v docker > /dev/null 2>&1 && ! live_restore_enabled; then
    record_change "apply $DAEMON_CONFIG, which Docker has not read yet (it reports live restore off)"
    apply=1
  fi
  if (( ! apply )); then
    return 0
  fi
  run dockerd --validate --config-file "$DAEMON_CONFIG"
  run systemctl reload docker.service
  if (( ! DRY_RUN )); then
    # Live restore applies on a reload, and only once it does may the restart that applies the log options run.
    local attempt
    for attempt in $(seq 1 30); do
      if live_restore_enabled; then
        break
      fi
      (( attempt < 30 )) || die "Docker did not turn live restore on after the reload, so it was not restarted"
      sleep 1
    done
  fi
  run systemctl restart docker.service
  if (( ! DRY_RUN )); then
    live_restore_enabled || die "Docker does not report live restore as enabled after its restart"
  fi
}

group_members() {
  getent group "$1" | cut -d: -f4
}

refuse_primary_group() {
  local group="$1" gid="$2"
  local -a primary=()
  # A service account without a login shell, such as the lxd account LXD's snap brings, is no way in for a person.
  mapfile -t primary < <(getent passwd | awk -F: -v gid="$gid" '$4 == gid && $7 !~ /(nologin|false)$/ { print $1 }')
  (( ${#primary[@]} == 0 )) || die "$group is the primary group of ${primary[*]}; give them another primary group first, because membership of $group is root access"
}

ensure_docker_group() {
  local entry
  if ! entry="$(getent group docker)"; then
    (( DRY_RUN )) || die "the docker group does not exist although Docker Engine is installed"
    record_change "make $DEPLOY_USER the only member of the docker group"
    return 0
  fi
  refuse_primary_group docker "$(cut -d: -f3 <<< "$entry")"
  local -a members=() others=()
  local member in_group=0
  IFS=, read -r -a members <<< "$(cut -d: -f4 <<< "$entry")"
  for member in "${members[@]}"; do
    if [[ "$member" == "$DEPLOY_USER" ]]; then
      in_group=1
    else
      others+=("$member")
    fi
  done
  for member in "${others[@]}"; do
    record_change "remove $member from the docker group, which is root access; $member runs Docker through sudo from now on"
    run gpasswd --delete "$member" docker > /dev/null
  done
  if (( ! in_group )); then
    record_change "add $DEPLOY_USER to the docker group"
    run usermod --append --groups docker "$DEPLOY_USER"
  fi
  if (( in_group && ${#others[@]} == 0 )); then
    log_info "in place: $DEPLOY_USER is the only member of the docker group"
  fi
}

ensure_deploy_account() {
  log_step "Deploy account $DEPLOY_USER"
  local entry created=0
  if entry="$(getent passwd "$DEPLOY_USER")"; then
    local home
    home="$(cut -d: -f6 <<< "$entry")"
    [[ "$home" == "$DEPLOY_HOME" ]] \
      || die "an account $DEPLOY_USER already exists with the home $home instead of $DEPLOY_HOME; this script did not create it, so rename or remove it first"
    log_info "in place: the account $DEPLOY_USER with the home $DEPLOY_HOME"
  else
    record_change "create the account $DEPLOY_USER with the home $DEPLOY_HOME and no password"
    run useradd --create-home --home-dir "$DEPLOY_HOME" --shell /bin/bash --user-group --password '*' \
      --comment 'ERP-AI-Pro deployments' "$DEPLOY_USER"
    created=1
  fi
  if is_root && getent passwd "$DEPLOY_USER" > /dev/null; then
    local password
    password="$(getent shadow "$DEPLOY_USER" | cut -d: -f2)"
    if [[ "$password" != '*' && "$password" != '!'* ]]; then
      record_change "remove the password of $DEPLOY_USER, which signs in with keys only"
      run usermod --password '*' "$DEPLOY_USER"
    fi
  fi
  if (( ! (created && DRY_RUN) )); then
    ensure_directory "$DEPLOY_HOME" 750 "$DEPLOY_USER:$DEPLOY_USER" "the home of $DEPLOY_USER"
  fi
  ensure_directory "$DEPLOY_HOME/.ssh" 700 "$DEPLOY_USER:$DEPLOY_USER" "the SSH folder of $DEPLOY_USER"
  install_config "$AUTHORIZED_KEYS_FILE" "$DEPLOY_HOME/.ssh/authorized_keys" 600 "$DEPLOY_USER:$DEPLOY_USER" \
    "the keys that sign in as $DEPLOY_USER" || true
  ensure_docker_group
}

ensure_no_lxd_access() {
  log_step "LXD: nobody gets root through it"
  local entry
  if entry="$(getent group lxd)"; then
    refuse_primary_group lxd "$(cut -d: -f3 <<< "$entry")"
    local -a members=()
    local member
    IFS=, read -r -a members <<< "$(cut -d: -f4 <<< "$entry")"
    for member in "${members[@]}"; do
      record_change "remove $member from the lxd group, whose members can install LXD and become root through it"
      run gpasswd --delete "$member" lxd > /dev/null
    done
    if (( ${#members[@]} == 0 )); then
      log_info "in place: the lxd group has no members"
    fi
  fi
  local state
  state="$(unit_state "$LXD_INSTALLER_SOCKET")"
  if [[ -n "$state" && "$state" != not-found && "$state" != masked ]]; then
    record_change "mask $LXD_INSTALLER_SOCKET, which installs LXD on demand for members of lxd"
    run systemctl mask --now --quiet "$LXD_INSTALLER_SOCKET"
  fi
}

ensure_env_file() {
  local target="$DEPLOY_COMPOSE_DIR/.env" source="$REPO_ROOT/infra/compose/.env.example"
  if [[ -f "$target" ]]; then
    local current
    current="$(sed -n 's/^ERP_SITE_ADDRESS=//p' "$target" | tail -n 1)"
    if [[ "$current" == "$SITE_ADDRESS" ]]; then
      log_info "in place: .env with ERP_SITE_ADDRESS=$SITE_ADDRESS ($target)"
    else
      log_warn "$target sets ERP_SITE_ADDRESS=${current:-<empty>}, not $SITE_ADDRESS; this script leaves .env to the deployments, and the address changes only together with the production sign-in settings (docs/operations/runbooks/proxy.md, section The address)"
    fi
    return 0
  fi
  [[ -f "$source" ]] || die "$source is missing from this checkout"
  grep -q '^ERP_SITE_ADDRESS=' "$source" || die "$source has no ERP_SITE_ADDRESS line to fill in"
  record_change ".env from .env.example with ERP_SITE_ADDRESS=$SITE_ADDRESS ($target)"
  if (( DRY_RUN )); then
    return 0
  fi
  local staged
  staged="$(mktemp)"
  sed "s|^ERP_SITE_ADDRESS=.*|ERP_SITE_ADDRESS=$SITE_ADDRESS|" "$source" > "$staged"
  install -m 600 -o "$DEPLOY_USER" -g "$DEPLOY_USER" -- "$staged" "$target" || die "cannot write $target"
  rm -f -- "$staged"
}

ensure_volumes() {
  if ! docker info > /dev/null 2>&1; then
    (( DRY_RUN )) || die "cannot reach the Docker daemon"
    record_change "create the Docker volumes ${STACK_VOLUMES[*]} that are missing (Docker could not be asked)"
    return 0
  fi
  local volume
  for volume in "${STACK_VOLUMES[@]}"; do
    if docker volume inspect "$volume" > /dev/null 2>&1; then
      log_info "in place: the Docker volume $volume"
    else
      record_change "create the Docker volume $volume, which is never removed"
      run docker volume create "$volume" > /dev/null
    fi
  done
}

ensure_checkout() {
  local commit
  commit="$(git -c safe.directory="$REPO_ROOT" -C "$REPO_ROOT" rev-parse HEAD)" || die "cannot read the commit of $REPO_ROOT; run this script from a git checkout"
  if [[ -d "$DEPLOY_CHECKOUT/.git" ]]; then
    local owners
    owners="$(stat -c %U -- "$DEPLOY_CHECKOUT" "$DEPLOY_CHECKOUT/.git" | sort -u | paste -sd, -)"
    if [[ "$owners" != "$DEPLOY_USER" ]]; then
      record_change "give the checkout back to $DEPLOY_USER (it belonged to $owners)"
      run chown -R -- "$DEPLOY_USER:$DEPLOY_USER" "$DEPLOY_CHECKOUT"
    fi
    log_info "in place: a checkout at $(git -c safe.directory="$DEPLOY_CHECKOUT" -C "$DEPLOY_CHECKOUT" rev-parse --short=12 HEAD), which deployments move and this script leaves"
    return 0
  fi
  [[ ! -e "$DEPLOY_CHECKOUT" ]] || die "$DEPLOY_CHECKOUT exists but is not a git checkout; move it away and run this script again"
  local origin partial="$DEPLOY_HOME/.ERP-AI-Pro.partial"
  origin="$(git -c safe.directory="$REPO_ROOT" -C "$REPO_ROOT" remote get-url origin)" || die "$REPO_ROOT has no remote origin for the deployment checkout to follow"
  if ! git -c safe.directory="$REPO_ROOT" -C "$REPO_ROOT" diff --quiet HEAD --; then
    log_warn "$REPO_ROOT has uncommitted changes, which the deployment checkout does not get"
  fi
  record_change "check out commit ${commit:0:12} of $origin in $DEPLOY_CHECKOUT"
  # The checkout takes its place only once it is complete and the deploy account's, so an interrupted run leaves no half checkout.
  run rm -rf -- "$partial"
  run git -c safe.directory="$REPO_ROOT" clone --quiet --no-local --no-checkout -- "$REPO_ROOT" "$partial"
  run git -C "$partial" remote set-url origin "$origin"
  run git -C "$partial" -c advice.detachedHead=false checkout --quiet --detach "$commit"
  run chown -R -- "$DEPLOY_USER:$DEPLOY_USER" "$partial"
  run mv -T -- "$partial" "$DEPLOY_CHECKOUT"
}

ensure_deployment_folder() {
  log_step "Deployment checkout $DEPLOY_CHECKOUT"
  require_command git
  if [[ -d "$DEPLOY_HOME" && ! -x "$DEPLOY_HOME" ]]; then
    record_change "the checkout, its secrets folder and .env in $DEPLOY_CHECKOUT (they could not be read without root, so a run with sudo checks them)"
    ensure_volumes
    return 0
  fi
  ensure_checkout
  ensure_directory "$DEPLOY_COMPOSE_DIR/secrets" 700 "$DEPLOY_USER:$DEPLOY_USER" "the folder of the client certificates"
  ensure_env_file
  ensure_volumes
}

ensure_units() {
  log_step "systemd units"
  local unit reload=0 timer_changed=0
  for unit in "${UNIT_FILES[@]}"; do
    if install_config "$HOST_CONFIG_DIR/systemd/$unit" "/etc/systemd/system/$unit" 644 root:root "the unit $unit"; then
      reload=1
      if [[ "$unit" == *.timer ]]; then
        timer_changed=1
      fi
    fi
  done
  if (( reload )); then
    run systemctl daemon-reload
  fi
  for unit in "${ENABLED_UNITS[@]}"; do
    if [[ "$(unit_state "$unit")" != enabled ]]; then
      record_change "enable $unit"
      run systemctl enable --quiet "$unit"
    fi
  done
  if ! systemctl is-active --quiet "$RETAIN_TIMER"; then
    record_change "start $RETAIN_TIMER"
    run systemctl start "$RETAIN_TIMER"
  elif (( timer_changed )); then
    run systemctl restart "$RETAIN_TIMER"
  fi
  if [[ -x "$DEPLOY_HOME" ]]; then
    local certificate
    local -a missing=()
    for certificate in "${CLIENT_CERTIFICATES[@]}"; do
      if [[ ! -f "$DEPLOY_COMPOSE_DIR/secrets/$certificate" ]]; then
        missing+=("$certificate")
      fi
    done
    if (( ${#missing[@]} > 0 )); then
      log_info "systemd skips both services until $DEPLOY_COMPOSE_DIR/secrets holds ${missing[*]}"
    fi
  fi
}

ensure_nightly_updates() {
  log_step "Security updates and the nightly restart"
  local source="$HOST_CONFIG_DIR/apt/52erp-ai-pro-unattended-upgrades.conf"
  install_config "$source" "$UPDATES_FILE" 644 root:root "unattended upgrades every day, restarting at 03:00 India time when an update needs it" || true
  local unit
  for unit in "${UPDATE_SERVICES[@]}" "${UPDATE_TIMERS[@]}"; do
    if [[ "$(unit_state "$unit")" == masked ]]; then
      record_change "unmask $unit, without which the daily updates never run"
      run systemctl unmask --quiet "$unit"
    fi
  done
  for unit in "${UPDATE_TIMERS[@]}"; do
    if [[ "$(unit_state "$unit")" != enabled ]]; then
      record_change "enable $unit"
      run systemctl enable --now --quiet "$unit"
    fi
  done
  if (( ! DRY_RUN )); then
    local effective setting
    effective="$(apt-config dump)"
    while IFS= read -r setting; do
      grep -qxF -- "$setting" <<< "$effective" || die "APT does not apply '$setting'; a file that sorts after $UPDATES_FILE sets it again"
    done < <(grep -vE '^[[:space:]]*(//|#|$)' "$source")
  fi
}

strict_modes_problem() {
  local path="$1" owner mode
  read -r owner mode < <(stat -c '%U %a' -- "$path")
  if [[ "$owner" != "$ADMIN_USER" && "$owner" != root ]]; then
    printf '%s belongs to %s' "$path" "$owner"
  elif (( (8#$mode & 8#022) != 0 )); then
    printf '%s has mode %s, which others may write' "$path" "$mode"
  fi
}

check_admin_key() {
  local admin_home admin_keys count problem path
  admin_home="$(getent passwd "$ADMIN_USER" | cut -d: -f6)"
  admin_keys="$(admin_keys_file)"
  if ! [[ -r "$admin_keys" ]] || ! count="$(valid_key_count "$admin_keys")" || (( count == 0 )); then
    die "$ADMIN_USER has no usable key in $admin_keys; add one from the administrator's computer (ssh-copy-id) before passwords are turned off"
  fi
  # sshd refuses keys whose files others may change (StrictModes), which would lock the administrator out once passwords are off.
  for path in "$admin_home" "$admin_home/.ssh" "$admin_keys"; do
    problem="$(strict_modes_problem "$path")"
    [[ -z "$problem" ]] || die "sshd would refuse $ADMIN_USER's keys because $problem; fix it (chmod go-w) and run this script again"
  done
  log_info "$ADMIN_USER keeps $count key(s) to sign in with ($admin_keys)"
}

ensure_ssh_keys_only() {
  log_step "SSH: public keys only, no root sign-in"
  check_admin_key
  if is_root; then
    local matches
    matches="$(grep -hsE '^[[:space:]]*Match[[:space:]]' /etc/ssh/sshd_config /etc/ssh/sshd_config.d/*.conf || true)"
    [[ -z "$matches" ]] || die "sshd's configuration holds Match blocks ($matches), whose settings this script cannot prove; remove them and run it again"
  fi

  local backup='' written=0
  if (( ! DRY_RUN )); then
    # sshd -t and -T need the privilege separation directory, which ssh.service creates only once it first starts.
    if [[ ! -d /run/sshd ]]; then
      install -d -m 0755 /run/sshd
    fi
    if [[ -f "$SSH_RULES_FILE" ]]; then
      backup="$(mktemp)"
      cp -p -- "$SSH_RULES_FILE" "$backup"
    fi
  fi
  if install_config "$HOST_CONFIG_DIR/ssh/10-erp-ai-pro.conf" "$SSH_RULES_FILE" 644 root:root "the SSH sign-in rules"; then
    written=1
  fi
  if (( DRY_RUN )); then
    return 0
  fi
  if ! sshd -t; then
    if (( written )) && [[ -n "$backup" ]]; then
      mv -f -- "$backup" "$SSH_RULES_FILE"
    elif (( written )); then
      rm -f -- "$SSH_RULES_FILE"
    fi
    die "sshd refuses its configuration (see above); any rules this run wrote were taken back"
  fi
  if [[ -n "$backup" ]]; then
    rm -f -- "$backup"
  fi
  # The running sshd keeps what it read at its last start, so every run reloads it, not only one that wrote the rules.
  systemctl try-reload-or-restart ssh.service

  local effective setting ports
  effective="$(sshd -T)"
  for setting in "${SSH_EFFECTIVE_SETTINGS[@]}"; do
    grep -qxF -- "$setting" <<< "$effective" \
      || die "sshd does not apply '$setting'; a file that sorts before $(basename -- "$SSH_RULES_FILE") in /etc/ssh/sshd_config.d, or sshd_config itself, sets it first"
  done
  ports="$(awk '$1 == "port" { print $2 }' <<< "$effective" | sort -u | paste -sd, -)"
  [[ "$ports" == 22 ]] || die "sshd listens on port(s) $ports, and the firewall admits SSH on port 22 only; move sshd back to port 22 and run this script again"
  log_info "sshd applies: ${SSH_EFFECTIVE_SETTINGS[*]}, on port 22"
}

ancestor_pids() {
  local pid="$$" parent
  while [[ "$pid" =~ ^[0-9]+$ ]] && (( pid > 1 )); do
    printf '%s\n' "$pid"
    parent="$(awk '$1 == "PPid:" { print $2 }' "/proc/$pid/status" 2> /dev/null || true)"
    pid="${parent:-0}"
  done
}

# sudo resets the environment and may run the command on a terminal of its own, but the shells that started it keep SSH_CONNECTION.
admin_session_address() {
  local pid connection
  while IFS= read -r pid; do
    connection="$(tr '\0' '\n' 2> /dev/null < "/proc/$pid/environ" | sed -n 's/^SSH_CONNECTION=//p' || true)"
    if [[ -n "$connection" ]]; then
      printf '%s' "${connection%% *}"
      return 0
    fi
  done < <(ancestor_pids)
  return 1
}

runs_through_sshd() {
  local pid
  while IFS= read -r pid; do
    case "$(cat "/proc/$pid/comm" 2> /dev/null || true)" in
      sshd | sshd-session | sshd-auth) return 0 ;;
    esac
  done < <(ancestor_pids)
  return 1
}

check_admin_session_address() {
  local address
  if address="$(admin_session_address)"; then
    if ! is_ipv4 "$address"; then
      die "this session comes from $address, and the firewall admits SSH over IPv4 from $HOME_NETWORK only; sign in through $SITE_ADDRESS and run the script again"
    fi
    ipv4_in_network "$address" "$HOME_NETWORK" \
      || die "this session comes from $address, outside $HOME_NETWORK, and the firewall would refuse its next sign-in; run the script from a device on the home network or pass --home-network"
    log_info "this session comes from $address, inside $HOME_NETWORK"
  elif runs_through_sshd; then
    die "this run comes through SSH, but the address of the session cannot be read; sign in to $SITE_ADDRESS from a device on the home network and run the script from that login shell"
  else
    log_info "this run does not come through SSH; the firewall admits SSH from $HOME_NETWORK"
  fi
}

ensure_firewall() {
  log_step "Firewall: new connections to SSH only, from $HOME_NETWORK only"
  if ! is_root; then
    record_change "deny new incoming connections except SSH from $HOME_NETWORK, on IPv4 and IPv6, and turn the firewall on (its state could not be read without root)"
    return 0
  fi
  check_admin_session_address

  local defaults=/etc/default/ufw ipv6_changed=0
  if ! grep -qx 'IPV6=yes' "$defaults"; then
    record_change "make the firewall cover IPv6 (IPV6=yes in $defaults)"
    if (( ! DRY_RUN )); then
      if grep -q '^IPV6=' "$defaults"; then
        sed -i 's/^IPV6=.*/IPV6=yes/' "$defaults"
      else
        printf 'IPV6=yes\n' >> "$defaults"
      fi
    fi
    ipv6_changed=1
  fi
  if ! grep -qx 'DEFAULT_INPUT_POLICY="DROP"' "$defaults"; then
    record_change "deny new incoming connections by default"
    run ufw_c default deny incoming > /dev/null
  fi
  if ! grep -qx 'DEFAULT_OUTPUT_POLICY="ACCEPT"' "$defaults"; then
    record_change "allow outgoing connections by default"
    run ufw_c default allow outgoing > /dev/null
  fi

  local added ours="from $HOME_NETWORK to any port 22 proto tcp"
  local -a others=()
  added="$(ufw_c show added | sed -n 's/^ufw //p')"
  mapfile -t others < <(grep -vF -- "$ours" <<< "$added" | grep -v '^$' || true)
  (( ${#others[@]} == 0 )) \
    || die "the firewall holds rules this host must not have: $(printf "'%s' " "${others[@]}")— remove each with sudo ufw delete <rule>, and run this script again"
  if grep -qF -- "$ours" <<< "$added"; then
    log_info "in place: SSH from $HOME_NETWORK"
  else
    record_change "allow SSH from $HOME_NETWORK"
    run ufw_c allow proto tcp from "$HOME_NETWORK" to any port 22 comment "$SSH_RULE_COMMENT" > /dev/null
  fi

  if [[ "$(ufw_c status)" == 'Status: active'* ]]; then
    log_info "in place: the firewall is on"
    if (( ipv6_changed )); then
      run ufw_c reload > /dev/null
    fi
  else
    record_change "turn the firewall on"
    run ufw_c --force enable > /dev/null
  fi

  if (( ! DRY_RUN )); then
    local family
    for family in iptables ip6tables; do
      [[ "$("$family" -S INPUT | sed -n 1p)" == '-P INPUT DROP' ]] || die "$family does not drop new incoming connections by default"
    done
  fi
}

print_summary() {
  log_step "Summary"
  if (( ${#CHANGES[@]} == 0 )); then
    printf 'nothing to change: the host already matches\n'
  elif (( DRY_RUN )); then
    printf 'a run would make %s change(s):\n' "${#CHANGES[@]}"
    printf '  - %s\n' "${CHANGES[@]}"
  else
    printf 'made %s change(s):\n' "${#CHANGES[@]}"
    printf '  - %s\n' "${CHANGES[@]}"
  fi
  log_info "next, as docs/operations/runbooks/host-setup.md describes: the client certificates in $DEPLOY_COMPOSE_DIR/secrets,"
  log_info "the remaining values of $DEPLOY_COMPOSE_DIR/.env, and deployments signed in as $DEPLOY_USER with its own key"
}

main() {
  parse_bootstrap_args "$@"
  require_command awk sed grep cmp comm sort stat install ip ssh-keygen systemctl timedatectl getent dpkg-query
  check_platform
  check_arguments
  ensure_packages
  ensure_docker_engine
  ensure_docker_daemon_config
  ensure_deploy_account
  ensure_no_lxd_access
  ensure_deployment_folder
  ensure_units
  ensure_nightly_updates
  ensure_ssh_keys_only
  ensure_firewall
  print_summary
}

main "$@"
