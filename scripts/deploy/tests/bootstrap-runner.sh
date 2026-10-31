#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/tests/bootstrap-runner.sh

Runs scripts/deploy/bootstrap.sh on the GitHub-hosted Ubuntu runner it runs on, after taking Docker and the
GitHub CLI off it so the bootstrap installs Docker Engine from Docker'"'"'s repository and gh from GitHub'"'"'s,
and with the json-file daemon.json that the bootstrap of ADR-0049 wrote in place: it proves that a deploy key
shared with the administrator and a backup certificate file that holds a private key are refused, that a dry
run without root and one with sudo change nothing, the latter listing the replacement of that daemon.json,
that the second of two runs with sudo and a dry run after it find nothing to change, and then that the deploy
account signs in with its own key, that the journal is on disk with the settings of its drop-in in effect,
that Docker runs with live restore and the journald log driver of the bootstrap'"'"'s daemon.json, that
docker.service may send the journal 100000 messages in 30 seconds, that a new container naming no log driver
logs to the journal and its line reaches /var/log/journal under its tag, that the deploy account runs gh
2.102.0 or later from GitHub'"'"'s repository with GitHub'"'"'s keys alone, that a password sign-in is refused,
and that sshd, the firewall on IPv4 and IPv6, the docker and lxd groups, the units (the backup timer among
them, its service skipped until the backup'"'"'s client certificate exists), the update services and
settings, the checkout with its secrets folder, .env and the backup'"'"'s public certificate, the
volumes and the production stack'"'"'s IPv4-only ports are what ADR-0049 and ADR-0058 decide. It hardens the
machine it runs on, so it refuses to run anywhere but on a GitHub-hosted runner, as the runner user.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=../lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/../lib/common.sh"

readonly RUNNER_DOCKER_PACKAGES=(docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin docker-ce-rootless-extras moby-engine moby-cli moby-containerd moby-runc moby-buildx moby-compose)
readonly UPDATE_SERVICES=(apt-daily.service apt-daily-upgrade.service)
readonly GITHUB_CLI_SOURCE='/etc/apt/sources.list.d/github-cli.sources'
readonly GITHUB_CLI_KEYRING='/etc/apt/keyrings/githubcli-archive-keyring.gpg'
readonly GITHUB_CLI_KEYS='2C6106201985B60E6C7AC87323F3D4EA75716059 7F38BBB59D064DBCB3D84D725612B36462313325'
readonly PLACEHOLDER_DIGEST='sha256:0000000000000000000000000000000000000000000000000000000000000000'
readonly BACKUP_RECIPIENT="$DEPLOY_COMPOSE_DIR/secrets/erp-backup-recipient.crt"
readonly JSON_FILE_DAEMON_CONFIG=$'{\n  "live-restore": true,\n  "log-driver": "json-file",\n  "log-opts": {\n    "max-size": "20m",\n    "max-file": "5"\n  }\n}'
readonly JOURNALD_SETTINGS='infra/host/journald/50-erp-ai-pro.conf'
readonly JOURNAL_USAGE_MESSAGE='ec387f577b844b8fa948f33cad9a75e6'
readonly LOG_CHECK_TAG='erp-ai-pro-log-check'

WORK_DIR=''
SITE_ADDRESS=''
HOME_NETWORK=''
SSH_OPTIONS=()

remove_work_dir() {
  if [[ -n "$WORK_DIR" ]]; then
    rm -rf -- "$WORK_DIR"
  fi
}

passed() {
  log_info "ok: $*"
}

remove_runner_docker() {
  local -a installed=()
  mapfile -t installed < <(dpkg-query -W -f='${Package} ${Status}\n' "${RUNNER_DOCKER_PACKAGES[@]}" 2> /dev/null | awk '$NF == "installed" { print $1 }')
  if (( ${#installed[@]} > 0 )); then
    sudo env DEBIAN_FRONTEND=noninteractive apt-get -qq purge -y "${installed[@]}" > /dev/null
  fi
  sudo rm -rf /usr/libexec/docker/cli-plugins /usr/local/lib/docker/cli-plugins /etc/docker/daemon.json
  sudo rm -f /etc/apt/sources.list.d/docker.list /etc/apt/sources.list.d/docker.sources /etc/apt/keyrings/docker.asc /etc/apt/keyrings/docker.gpg
  if command -v docker > /dev/null 2>&1; then
    die "Docker is still on the runner ($(command -v docker)), so the bootstrap's install would not run"
  fi
  log_info "Docker is off the runner, so the bootstrap installs it"
}

remove_runner_github_cli() {
  if [[ "$(dpkg-query -W -f='${Status}' gh 2> /dev/null || true)" == 'install ok installed' ]]; then
    sudo env DEBIAN_FRONTEND=noninteractive apt-get -qq purge -y gh > /dev/null
  fi
  sudo rm -f /etc/apt/sources.list.d/github-cli.list "$GITHUB_CLI_SOURCE" "$GITHUB_CLI_KEYRING" /usr/share/keyrings/githubcli-archive-keyring.gpg
  if command -v gh > /dev/null 2>&1; then
    die "the GitHub CLI is still on the runner ($(command -v gh)), so the bootstrap's install would not run"
  fi
  log_info "the GitHub CLI is off the runner, so the bootstrap installs it"
}

prepare_runner() {
  log_step "Preparing the runner"
  ssh-keygen -q -t ed25519 -N '' -C bootstrap-test-admin -f "$WORK_DIR/admin"
  ssh-keygen -q -t ed25519 -N '' -C bootstrap-test-deploy -f "$WORK_DIR/deploy"
  openssl req -x509 -newkey rsa:4096 -noenc -days 2 -subj '/CN=bootstrap-test-backup-recipient' \
    -keyout "$WORK_DIR/recipient-key.pem" -out "$WORK_DIR/recipient.crt" 2> /dev/null
  cat "$WORK_DIR/recipient-key.pem" "$WORK_DIR/recipient.crt" > "$WORK_DIR/recipient-bundle.pem"
  chmod go-w "$HOME"
  install -d -m 700 "$HOME/.ssh"
  cat "$WORK_DIR/admin.pub" >> "$HOME/.ssh/authorized_keys"
  chmod 600 "$HOME/.ssh/authorized_keys"
  remove_runner_docker
  remove_runner_github_cli
  printf '%s\n' "$JSON_FILE_DAEMON_CONFIG" > "$WORK_DIR/json-file-daemon.json"
  sudo install -D -m 644 -o root -g root -- "$WORK_DIR/json-file-daemon.json" /etc/docker/daemon.json
  # The runner must not upgrade or restart itself during the test, and masking the services proves that the bootstrap unmasks them.
  local unit
  for unit in "${UPDATE_SERVICES[@]}"; do
    sudo install -d "/etc/systemd/system/$unit.d"
    printf '[Service]\nExecStartPre=\nExecStart=\nExecStart=/bin/true\n' | sudo tee "/etc/systemd/system/$unit.d/zz-bootstrap-test.conf" > /dev/null
    sudo systemctl mask --quiet "$unit"
  done
  sudo systemctl daemon-reload
  SITE_ADDRESS="$(ip -4 route get 1.1.1.1 | sed -n 's/.* src \([0-9.]*\).*/\1/p')"
  is_ipv4 "$SITE_ADDRESS" || die "cannot find the runner's IPv4 address"
  local prefix
  prefix="$(ip -4 -o address show | awk -v address="$SITE_ADDRESS" '{ split($4, parts, "/") } parts[1] == address && !found { print parts[2]; found = 1 }')"
  HOME_NETWORK="$(ipv4_network_of "$SITE_ADDRESS" "$prefix")"
  log_info "the runner's address $SITE_ADDRESS stands for the site address, $HOME_NETWORK for the home network"
}

assert_untouched() {
  local after="$1"
  if getent passwd "$DEPLOY_USER" > /dev/null || [[ -e "$DEPLOY_HOME" ]]; then
    die "$after created the account $DEPLOY_USER or $DEPLOY_HOME"
  fi
  local path
  for path in /etc/ssh/sshd_config.d/10-erp-ai-pro.conf /etc/systemd/system/erp-ai-pro.service \
    /etc/apt/apt.conf.d/52erp-ai-pro-unattended-upgrades.conf /etc/systemd/journald.conf.d/50-erp-ai-pro.conf \
    /etc/systemd/system/docker.service.d/50-erp-ai-pro.conf "$GITHUB_CLI_SOURCE" "$GITHUB_CLI_KEYRING"; do
    [[ ! -e "$path" ]] || die "$after wrote $path"
  done
  cmp -s -- "$WORK_DIR/json-file-daemon.json" /etc/docker/daemon.json || die "$after changed /etc/docker/daemon.json"
  if command -v docker > /dev/null 2>&1; then
    die "$after installed Docker"
  fi
  if command -v gh > /dev/null 2>&1; then
    die "$after installed the GitHub CLI"
  fi
  [[ "$(sudo env LC_ALL=C ufw status)" == 'Status: inactive'* ]] || die "$after turned the firewall on"
  [[ "$(systemctl is-enabled apt-daily-upgrade.service 2> /dev/null || true)" == masked ]] || die "$after unmasked the update services"
}

run_bootstrap() {
  local -a arguments=(--site-address "$SITE_ADDRESS" --authorized-keys "$WORK_DIR/deploy.pub" --backup-recipient "$WORK_DIR/recipient.crt")
  local summary

  log_step "A deploy key shared with the administrator is refused"
  if summary="$(sudo bash scripts/deploy/bootstrap.sh --dry-run --site-address "$SITE_ADDRESS" --authorized-keys "$HOME/.ssh/authorized_keys" 2>&1)"; then
    die "the bootstrap accepted the administrator's own keys for $DEPLOY_USER"
  fi
  [[ "$summary" == *'needs a key of its own'* ]] || die "the bootstrap refused the shared key for another reason: $summary"
  passed "a deploy key that also signs in as the administrator is refused"

  log_step "A backup certificate file that holds a private key is refused"
  if summary="$(sudo bash scripts/deploy/bootstrap.sh --dry-run --site-address "$SITE_ADDRESS" --authorized-keys "$WORK_DIR/deploy.pub" \
    --backup-recipient "$WORK_DIR/recipient-bundle.pem" 2>&1)"; then
    die "the bootstrap accepted a backup certificate file that holds a private key"
  fi
  [[ "$summary" == *'holds a private key, which never belongs on the server'* ]] || die "the bootstrap refused the key file for another reason: $summary"
  passed "a backup certificate file that holds a private key is refused"

  log_step "A dry run without root changes nothing"
  summary="$(bash scripts/deploy/bootstrap.sh --dry-run "${arguments[@]}")"
  [[ "$summary" =~ ^a\ run\ would\ make\ [0-9]+\ change\(s\): ]] || die "the dry run without root listed no changes: $summary"
  assert_untouched "the dry run without root"
  passed "the dry run without root listed $(grep -c '^  - ' <<< "$summary") changes and made none"

  log_step "A dry run with sudo changes nothing"
  summary="$(sudo bash scripts/deploy/bootstrap.sh --dry-run "${arguments[@]}")"
  [[ "$summary" =~ ^a\ run\ would\ make\ [0-9]+\ change\(s\): ]] || die "the dry run with sudo listed no changes: $summary"
  grep -qF 'unmask apt-daily-upgrade.service' <<< "$summary" || die "the dry run with sudo did not notice the masked update service: $summary"
  grep -qF 'in place of the json-file log rotation this script wrote' <<< "$summary" \
    || die "the dry run with sudo did not list the replacement of the json-file daemon.json: $summary"
  assert_untouched "the dry run with sudo"
  passed "the dry run with sudo listed $(grep -c '^  - ' <<< "$summary") changes, the masked update services and the json-file daemon.json among them, and made none"

  log_step "The first run"
  sudo bash scripts/deploy/bootstrap.sh "${arguments[@]}"

  log_step "The second run and a dry run after it change nothing"
  summary="$(sudo bash scripts/deploy/bootstrap.sh "${arguments[@]}")"
  [[ "$summary" == 'nothing to change: the host already matches' ]] || die "the second run changed something: $summary"
  summary="$(sudo bash scripts/deploy/bootstrap.sh --dry-run "${arguments[@]}")"
  [[ "$summary" == 'nothing to change: the host already matches' ]] || die "the dry run after the second run lists changes: $summary"
  passed "the second run and the dry run after it found nothing to change"
}

prove_sign_in() {
  log_step "Sign-in"
  [[ "$(ssh "${SSH_OPTIONS[@]}" -i "$WORK_DIR/deploy" "$DEPLOY_USER@127.0.0.1" "docker info --format '{{.LiveRestoreEnabled}}'")" == true ]] \
    || die "$DEPLOY_USER cannot sign in with its key and reach Docker with live restore"
  passed "$DEPLOY_USER signs in with its own key and reaches Docker, which keeps containers running across its own restarts"
  local refusal
  refusal="$(ssh "${SSH_OPTIONS[@]}" -o PubkeyAuthentication=no -o PreferredAuthentications=password,keyboard-interactive "$USER@127.0.0.1" true 2>&1 || true)"
  [[ "$refusal" == *'Permission denied (publickey).'* ]] || die "a password sign-in was not refused with public keys as the only method: $refusal"
  passed "a password sign-in is refused, and sshd offers public keys only"
}

prove_journal() {
  log_step "The journal on disk, with its drop-in's settings in effect"
  local effective setting use keep_free usage
  effective="$(systemd-analyze --no-pager cat-config systemd/journald.conf | awk '
    /^[ \t]*\[/ { section = $0; gsub(/[ \t]/, "", section); next }
    /^[ \t]*([#;]|$)/ || index($0, "=") == 0 || section != "[Journal]" { next }
    {
      key = substr($0, 1, index($0, "=") - 1)
      value = substr($0, index($0, "=") + 1)
      gsub(/^[ \t]+|[ \t]+$/, "", key)
      gsub(/^[ \t]+|[ \t]+$/, "", value)
      last[key] = value
    }
    END { for (key in last) print key "=" last[key] }')"
  while IFS= read -r setting; do
    grep -qxF -- "$setting" <<< "$effective" || die "journald's configuration does not end with '$setting': $(paste -sd ' ' - <<< "$effective")"
  done < <(grep -vE '^[[:space:]]*([#;[]|$)' "$JOURNALD_SETTINGS")
  use="$(sed -n 's/^SystemMaxUse=\([0-9]*\)G$/\1/p' "$JOURNALD_SETTINGS")"
  keep_free="$(sed -n 's/^SystemKeepFree=\([0-9]*\)G$/\1/p' "$JOURNALD_SETTINGS")"
  # A flush makes journald report the system journal's path and the limits it runs with, in bytes.
  sudo journalctl --flush
  usage="$(sudo journalctl --no-pager --quiet --output=json --lines=1 "MESSAGE_ID=$JOURNAL_USAGE_MESSAGE" 'JOURNAL_NAME=System Journal' \
    | jq -r '"\(.JOURNAL_PATH) \(.MAX_USE) \(.DISK_KEEP_FREE)"')"
  [[ "$usage" == "/var/log/journal/"*" $(( use * 1073741824 )) $(( keep_free * 1073741824 ))" ]] \
    || die "journald does not run the system journal under /var/log/journal with at most $use GB and $keep_free GB of the disk left free: $usage"
  passed "journald keeps the system journal on disk at ${usage%% *}, at most $use GB with $keep_free GB left free, and applies every setting of $JOURNALD_SETTINGS"
}

prove_docker() {
  log_step "Docker Engine from Docker's repository, logging to the journal"
  local policy
  policy="$(apt-cache policy docker-ce)"
  grep -qF 'download.docker.com' <<< "$policy" || die "docker-ce does not come from Docker's repository: $policy"
  [[ "$(dpkg-query -W -f='${Status}' docker-compose-plugin 2> /dev/null || true)" == 'install ok installed' ]] || die "docker-compose-plugin is not installed"
  cmp -s -- infra/host/docker/daemon.json /etc/docker/daemon.json \
    || die "/etc/docker/daemon.json is not infra/host/docker/daemon.json: $(< /etc/docker/daemon.json)"
  [[ "$(sudo docker info --format '{{.LiveRestoreEnabled}} {{.LoggingDriver}}')" == 'true journald' ]] \
    || die "Docker does not run with live restore and the journald log driver"
  passed "Docker Engine and Compose come from Docker's repository, and the bootstrap's daemon.json took the json-file one's place: live restore and the journald log driver"

  local interval burst
  interval="$(systemctl show --property=LogRateLimitIntervalUSec --value docker.service)"
  burst="$(systemctl show --property=LogRateLimitBurst --value docker.service)"
  [[ "$interval $burst" == '30s 100000' ]] || die "docker.service may send the journal $burst messages in $interval, not 100000 in 30s"
  passed "docker.service, through which every container logs, may send the journal 100000 messages in 30 seconds"

  local image marker logging lines attempt
  image="$(awk '$1 == "image:" && $2 ~ /^mcr\.microsoft\.com\/azurelinux\/distroless\/debug:/ { print $2; exit }' infra/compose/compose.backup.yaml)"
  [[ -n "$image" ]] || die "cannot read the BusyBox image of infra/compose/compose.backup.yaml"
  marker="$LOG_CHECK_TAG $(openssl rand -hex 8)"
  sudo docker run --name "$LOG_CHECK_TAG" --network none --log-opt "tag=$LOG_CHECK_TAG" --entrypoint /usr/sbin/busybox "$image" \
    echo "$marker" > /dev/null
  logging="$(sudo docker inspect --format '{{.HostConfig.LogConfig.Type}}' "$LOG_CHECK_TAG")"
  sudo docker rm "$LOG_CHECK_TAG" > /dev/null
  [[ "$logging" == journald ]] || die "a new container that names no log driver logs with '$logging' instead of journald"
  for attempt in 1 2 3 4 5 6 7 8 9 10; do
    sudo journalctl --sync
    lines="$(sudo journalctl --directory=/var/log/journal --no-pager --quiet --output=cat "CONTAINER_TAG=$LOG_CHECK_TAG")"
    if grep -qxF -- "$marker" <<< "$lines"; then
      break
    fi
    (( attempt < 10 )) || die "the container's line '$marker' did not reach /var/log/journal under the tag $LOG_CHECK_TAG: $lines"
    sleep 1
  done
  passed "a new container that names no log driver logs to the journal, and its line is on disk in /var/log/journal under its tag"
}

prove_github_cli() {
  log_step "The GitHub CLI from GitHub's repository"
  local expected fingerprints fingerprint version output
  printf -v expected 'Types: deb\nURIs: https://cli.github.com/packages\nSuites: stable\nComponents: main\nArchitectures: %s\nSigned-By: %s' \
    "$(dpkg --print-architecture)" "$GITHUB_CLI_KEYRING"
  [[ "$(< "$GITHUB_CLI_SOURCE")" == "$expected" ]] || die "$GITHUB_CLI_SOURCE holds: $(< "$GITHUB_CLI_SOURCE")"
  [[ "$(stat -c '%a %U:%G' "$GITHUB_CLI_SOURCE" "$GITHUB_CLI_KEYRING" | sort -u)" == '644 root:root' ]] \
    || die "$GITHUB_CLI_SOURCE and $GITHUB_CLI_KEYRING are not root's with mode 644"
  install -d -m 700 "$WORK_DIR/gnupg"
  fingerprints="$(gpg --batch --quiet --homedir "$WORK_DIR/gnupg" --with-colons --show-keys -- "$GITHUB_CLI_KEYRING" \
    | awk -F: '$1 == "pub" { primary = 1; next } $1 == "fpr" && primary { print $10 } { primary = 0 }')"
  gpgconf --homedir "$WORK_DIR/gnupg" --kill all > /dev/null 2>&1 || true
  grep -qxF 7F38BBB59D064DBCB3D84D725612B36462313325 <<< "$fingerprints" || die "$GITHUB_CLI_KEYRING lacks GitHub's signing key: $fingerprints"
  while IFS= read -r fingerprint; do
    [[ " $GITHUB_CLI_KEYS " == *" $fingerprint "* ]] || die "$GITHUB_CLI_KEYRING holds the key $fingerprint, which GitHub does not publish"
  done <<< "$fingerprints"
  apt-cache policy gh | awk 'found && /^        / { print; next } found { exit } /^ \*\*\* / { found = 1 }' \
    | grep -qF 'https://cli.github.com/packages stable/main' || die "the installed gh does not come from GitHub's repository: $(apt-cache policy gh)"
  version="$(dpkg-query -W -f='${Version}' gh)"
  dpkg --compare-versions "$version" ge "$MINIMUM_GH_VERSION" || die "gh $version is older than $MINIMUM_GH_VERSION"
  output="$(ssh "${SSH_OPTIONS[@]}" -i "$WORK_DIR/deploy" "$DEPLOY_USER@127.0.0.1" 'env -i HOME="$HOME" PATH="$PATH" gh --version')" \
    || die "$DEPLOY_USER cannot run gh as deploy.sh runs it"
  [[ "${output%%$'\n'*}" == "gh version $version "* ]] || die "$DEPLOY_USER runs another gh than the packaged $version: $output"
  passed "$DEPLOY_USER runs gh $version from GitHub's repository, signed with GitHub's keys alone ($(paste -sd ' ' - <<< "$fingerprints"))"
}

prove_access_rules() {
  log_step "Groups, sshd and the firewall"
  local members
  members="$(getent group docker | cut -d: -f4)"
  [[ "$members" == "$DEPLOY_USER" ]] || die "the docker group holds '$members', not $DEPLOY_USER alone"
  if getent group lxd > /dev/null; then
    [[ -z "$(getent group lxd | cut -d: -f4)" ]] || die "the lxd group still has members: $(getent group lxd)"
  fi
  local socket
  socket="$(systemctl is-enabled lxd-installer.socket 2> /dev/null || true)"
  [[ -z "$socket" || "$socket" == not-found || "$socket" == masked ]] || die "lxd-installer.socket is $socket, not masked"
  passed "$DEPLOY_USER is the only member of docker, lxd has no member and LXD cannot be installed on demand"

  local effective setting
  effective="$(sudo sshd -T)"
  for setting in 'passwordauthentication no' 'kbdinteractiveauthentication no' 'authenticationmethods publickey' 'permitrootlogin no' 'x11forwarding no'; do
    grep -qxF -- "$setting" <<< "$effective" || die "sshd does not apply '$setting'"
  done
  passed "sshd applies public keys only and no root sign-in"

  local firewall rules
  firewall="$(sudo env LC_ALL=C ufw status verbose)"
  grep -qx 'Status: active' <<< "$firewall" || die "the firewall is not on: $firewall"
  grep -q '^Default: deny (incoming), allow (outgoing)' <<< "$firewall" || die "the firewall's defaults differ: $firewall"
  rules="$(sudo env LC_ALL=C ufw show added | sed -n 's/^ufw //p')"
  [[ "$rules" == "allow from $HOME_NETWORK to any port 22 proto tcp comment 'SSH from the home network'" ]] \
    || die "the firewall holds other rules than SSH from $HOME_NETWORK: $rules"
  local family
  for family in iptables ip6tables; do
    [[ "$(sudo "$family" -S INPUT | sed -n 1p)" == '-P INPUT DROP' ]] || die "$family does not drop new incoming connections"
  done
  passed "the firewall holds one rule, SSH from $HOME_NETWORK, and drops every other new connection over IPv4 and IPv6"
}

prove_units_and_updates() {
  log_step "The units and the nightly updates"
  local unit
  for unit in erp-ai-pro.service erp-ai-pro-retain.timer erp-ai-pro-heal.timer erp-ai-pro-backup.timer; do
    [[ "$(systemctl is-enabled "$unit")" == enabled ]] || die "$unit is not enabled"
  done
  for unit in erp-ai-pro-retain.timer erp-ai-pro-heal.timer erp-ai-pro-backup.timer; do
    systemctl is-active --quiet "$unit" || die "$unit is not running"
  done
  for unit in erp-ai-pro.service erp-ai-pro-retain.service erp-ai-pro-backup.service; do
    sudo systemctl start "$unit"
    [[ "$(systemctl show --property=ConditionResult --value "$unit")" == no ]] || die "$unit did not wait for .env and its certificates"
  done
  passed "the boot unit and the three timers are enabled and running, and the boot, retention and backup services wait for their certificates"

  sudo systemctl start erp-ai-pro-heal.service
  [[ "$(systemctl show --property=Result --value erp-ai-pro-heal.service)" == success ]] \
    || die "erp-ai-pro-heal.service did not succeed on a server whose stack is not deployed: $(sudo journalctl -u erp-ai-pro-heal.service --no-pager -n 20)"
  passed "erp-ai-pro-heal.service runs heal.sh as $DEPLOY_USER and finds nothing to restart before the first deployment"

  for unit in "${UPDATE_SERVICES[@]}" apt-daily.timer apt-daily-upgrade.timer; do
    [[ "$(systemctl is-enabled "$unit" 2> /dev/null || true)" != masked ]] || die "$unit is still masked"
  done
  local effective setting
  effective="$(apt-config dump)"
  while IFS= read -r setting; do
    grep -qxF -- "$setting" <<< "$effective" || die "APT does not apply '$setting'"
  done < <(grep -vE '^[[:space:]]*(//|#|$)' infra/host/apt/52erp-ai-pro-unattended-upgrades.conf)
  passed "the update services and timers run, and unattended upgrades restart the host at 21:30 UTC when an update needs it"
}

prove_deployment_folder() {
  log_step "The deployment checkout and the volumes"
  [[ "$(sudo git -c safe.directory="$DEPLOY_CHECKOUT" -C "$DEPLOY_CHECKOUT" rev-parse HEAD)" == "$(git rev-parse HEAD)" ]] \
    || die "$DEPLOY_CHECKOUT is not at the commit the bootstrap ran from"
  [[ "$(sudo stat -c '%U:%G' "$DEPLOY_CHECKOUT" "$DEPLOY_CHECKOUT/.git" "$DEPLOY_CHECKOUT/README.md" | sort -u)" == "$DEPLOY_USER:$DEPLOY_USER" ]] \
    || die "$DEPLOY_CHECKOUT does not belong to $DEPLOY_USER"
  [[ ! -e "$DEPLOY_HOME/.ERP-AI-Pro.partial" ]] || die "the partial checkout was left behind"
  [[ "$(sudo stat -c '%a %U:%G' "$DEPLOY_COMPOSE_DIR/secrets")" == "700 $DEPLOY_USER:$DEPLOY_USER" ]] || die "the secrets folder is not private to $DEPLOY_USER"
  [[ "$(sudo stat -c '%a %U:%G' "$DEPLOY_COMPOSE_DIR/.env")" == "600 $DEPLOY_USER:$DEPLOY_USER" ]] || die ".env is not private to $DEPLOY_USER"
  sudo grep -qxF "ERP_SITE_ADDRESS=$SITE_ADDRESS" "$DEPLOY_COMPOSE_DIR/.env" || die ".env does not name ERP_SITE_ADDRESS=$SITE_ADDRESS"
  [[ "$(sudo stat -c '%a %U:%G' "$BACKUP_RECIPIENT")" == '644 root:root' ]] || die "$BACKUP_RECIPIENT is not root's with mode 644"
  sudo cmp -s -- "$WORK_DIR/recipient.crt" "$BACKUP_RECIPIENT" || die "$BACKUP_RECIPIENT is not the certificate passed with --backup-recipient"
  passed "$DEPLOY_CHECKOUT is at this commit and belongs to $DEPLOY_USER, with a private secrets folder, .env and the backup's public certificate"
  local volume
  for volume in "${STACK_VOLUMES[@]}"; do
    sudo docker volume inspect "$volume" > /dev/null || die "the volume $volume is missing"
  done
  passed "the volumes ${STACK_VOLUMES[*]} exist"
}

prove_ipv4_ports() {
  log_step "The production stack publishes its ports on IPv4 only"
  local ports
  ports="$(cd infra/compose && ERP_SITE_ADDRESS="$SITE_ADDRESS" IMAGE_TAG=0.0.0 API_IMAGE_DIGEST="$PLACEHOLDER_DIGEST" \
    WEB_IMAGE_DIGEST="$PLACEHOLDER_DIGEST" PROXY_IMAGE_DIGEST="$PLACEHOLDER_DIGEST" \
    docker compose -f compose.yaml -f compose.production.yaml config --format json \
    | jq -r '.services | to_entries[] | .key as $service | (.value.ports // [])[] | "\($service) \(.host_ip // "*") \(.published) \(.target)"')"
  [[ "$ports" == $'proxy 0.0.0.0 80 80\nproxy 0.0.0.0 443 443' ]] || die "the production stack publishes: $ports"
  passed "only the proxy publishes ports, 80 and 443 on 0.0.0.0"
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  [[ "${GITHUB_ACTIONS:-}" == true && "${RUNNER_ENVIRONMENT:-}" == github-hosted ]] \
    || die "this test hardens the machine it runs on, so it runs only on a GitHub-hosted runner"
  if is_root; then
    die "run it as the runner user; it calls sudo where it needs root"
  fi
  require_command sudo ssh ssh-keygen jq git apt-cache gpg gpgconf openssl systemd-analyze journalctl
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  WORK_DIR="$(mktemp -d)"
  trap remove_work_dir EXIT
  SSH_OPTIONS=(-o BatchMode=yes -o StrictHostKeyChecking=accept-new -o "UserKnownHostsFile=$WORK_DIR/known_hosts" -o ConnectTimeout=10)

  prepare_runner
  run_bootstrap
  prove_sign_in
  prove_journal
  prove_docker
  prove_github_cli
  prove_access_rules
  prove_units_and_updates
  prove_deployment_folder
  prove_ipv4_ports
  printf 'host bootstrap test ok\n'
}

main "$@"
