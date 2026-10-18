#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/tests/bootstrap-runner.sh

Runs scripts/deploy/bootstrap.sh on the GitHub-hosted Ubuntu runner it runs on, first as a dry run
without root and then twice with sudo, and proves the result: the dry run changes nothing, the second
run finds nothing to change, the deploy account signs in with its key and reaches Docker with live
restore, a password sign-in is refused, and sshd, the firewall, the docker group, the units, the APT
settings, the checkout with its secrets folder and .env, and the volumes are what the bootstrap
promises; it also proves that the production stack publishes its ports on IPv4 only. It hardens the
machine it runs on, so it refuses to run anywhere but on a GitHub-hosted runner, as the runner user.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=../lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/../lib/common.sh"

WORK_DIR=''
SITE_ADDRESS=''
HOME_NETWORK=''

remove_work_dir() {
  if [[ -n "$WORK_DIR" ]]; then
    rm -rf -- "$WORK_DIR"
  fi
}

passed() {
  log_info "ok: $*"
}

prepare_runner() {
  log_step "Preparing the runner"
  ssh-keygen -q -t ed25519 -N '' -C bootstrap-test-admin -f "$WORK_DIR/admin"
  ssh-keygen -q -t ed25519 -N '' -C bootstrap-test-deploy -f "$WORK_DIR/deploy"
  install -d -m 700 "$HOME/.ssh"
  cat "$WORK_DIR/admin.pub" >> "$HOME/.ssh/authorized_keys"
  chmod 600 "$HOME/.ssh/authorized_keys"
  # The bootstrap refuses to merge another daemon.json, and the runner image brings its own.
  sudo rm -f /etc/docker/daemon.json
  sudo systemctl restart docker.service
  # The update timers the bootstrap enables must not upgrade, or schedule a restart of, the runner while the test runs.
  sudo systemctl mask --quiet apt-daily.service apt-daily-upgrade.service
  SITE_ADDRESS="$(ip -4 route get 1.1.1.1 | sed -n 's/.* src \([0-9.]*\).*/\1/p')"
  is_ipv4 "$SITE_ADDRESS" || die "cannot find the runner's IPv4 address"
  local prefix
  prefix="$(ip -4 -o address show | awk -v address="$SITE_ADDRESS" '{ split($4, parts, "/") } parts[1] == address && !found { print parts[2]; found = 1 }')"
  HOME_NETWORK="$(ipv4_network_of "$SITE_ADDRESS" "$prefix")"
  log_info "the runner's address $SITE_ADDRESS stands for the site address, $HOME_NETWORK for the home network"
}

run_bootstrap() {
  local -a arguments=(--site-address "$SITE_ADDRESS" --authorized-keys "$WORK_DIR/deploy.pub")

  log_step "A dry run without root changes nothing"
  bash scripts/deploy/bootstrap.sh --dry-run "${arguments[@]}" > "$WORK_DIR/dry-run.txt"
  grep -qE '^a run would make [0-9]+ change\(s\):$' "$WORK_DIR/dry-run.txt" || die "the dry run listed no changes: $(cat "$WORK_DIR/dry-run.txt")"
  if getent passwd "$DEPLOY_USER" > /dev/null || [[ -e "$DEPLOY_HOME" ]]; then
    die "the dry run created the account $DEPLOY_USER or $DEPLOY_HOME"
  fi
  passed "the dry run listed $(grep -c '^  - ' "$WORK_DIR/dry-run.txt") changes and made none"

  log_step "The first run"
  sudo bash scripts/deploy/bootstrap.sh "${arguments[@]}"

  log_step "The second run changes nothing"
  local summary
  summary="$(sudo bash scripts/deploy/bootstrap.sh "${arguments[@]}")"
  [[ "$summary" == 'nothing to change: the host already matches' ]] || die "the second run changed something: $summary"
  passed "the second run found nothing to change"
}

prove_sign_in() {
  log_step "Sign-in"
  local -a options=(-o BatchMode=yes -o StrictHostKeyChecking=accept-new -o "UserKnownHostsFile=$WORK_DIR/known_hosts" -o ConnectTimeout=10)
  [[ "$(ssh "${options[@]}" -i "$WORK_DIR/deploy" "$DEPLOY_USER@127.0.0.1" "docker info --format '{{.LiveRestoreEnabled}}'")" == true ]] \
    || die "$DEPLOY_USER cannot sign in with its key and reach Docker with live restore"
  passed "$DEPLOY_USER signs in with its key and reaches Docker, which keeps containers running across its own restarts"
  local refusal
  refusal="$(ssh "${options[@]}" -o PubkeyAuthentication=no -o PreferredAuthentications=password,keyboard-interactive "$USER@127.0.0.1" true 2>&1 || true)"
  [[ "$refusal" == *'Permission denied (publickey).'* ]] || die "a password sign-in was not refused with public keys as the only method: $refusal"
  passed "a password sign-in is refused, and sshd offers public keys only"
}

prove_access_rules() {
  log_step "The docker group, sshd and the firewall"
  local members
  members="$(getent group docker | cut -d: -f4)"
  [[ "$members" == "$DEPLOY_USER" ]] || die "the docker group holds '$members', not $DEPLOY_USER alone"
  passed "$DEPLOY_USER is the only member of the docker group"

  local effective setting
  effective="$(sudo sshd -T)"
  for setting in 'passwordauthentication no' 'kbdinteractiveauthentication no' 'authenticationmethods publickey' 'permitrootlogin no' 'x11forwarding no'; do
    grep -qxF -- "$setting" <<< "$effective" || die "sshd does not apply '$setting'"
  done
  passed "sshd applies public keys only and no root sign-in"

  local firewall
  firewall="$(sudo ufw status verbose)"
  grep -qx 'Status: active' <<< "$firewall" || die "the firewall is not on: $firewall"
  grep -q '^Default: deny (incoming), allow (outgoing)' <<< "$firewall" || die "the firewall's defaults differ: $firewall"
  local allowed
  allowed="$(awk '$2 == "ALLOW" && $3 == "IN" { print $1, $4 }' <<< "$firewall")"
  [[ "$allowed" == "22/tcp $HOME_NETWORK" ]] || die "the firewall admits '$allowed' instead of SSH from $HOME_NETWORK alone: $firewall"
  passed "the firewall admits new connections to SSH from $HOME_NETWORK only, on IPv4 and IPv6"
}

prove_units_and_updates() {
  log_step "The units and the nightly updates"
  local unit
  for unit in erp-ai-pro.service erp-ai-pro-retain.timer; do
    [[ "$(systemctl is-enabled "$unit")" == enabled ]] || die "$unit is not enabled"
  done
  systemctl is-active --quiet erp-ai-pro-retain.timer || die "erp-ai-pro-retain.timer is not running"
  for unit in erp-ai-pro.service erp-ai-pro-retain.service; do
    sudo systemctl start "$unit"
    [[ "$(systemctl show --property=ConditionResult --value "$unit")" == no ]] || die "$unit did not wait for .env and the client certificates"
  done
  passed "the boot unit and the daily timer are enabled, and both services wait for the client certificates"

  local effective setting
  effective="$(apt-config dump)"
  while IFS= read -r setting; do
    grep -qxF -- "$setting" <<< "$effective" || die "APT does not apply '$setting'"
  done < <(grep -vE '^[[:space:]]*(//|$)' infra/host/apt/52erp-ai-pro-unattended-upgrades)
  passed "unattended upgrades run every day and restart the host at 21:30 UTC when an update needs it"
}

prove_deployment_folder() {
  log_step "The deployment checkout and the volumes"
  [[ "$(sudo git -c safe.directory="$DEPLOY_CHECKOUT" -C "$DEPLOY_CHECKOUT" rev-parse HEAD)" == "$(git rev-parse HEAD)" ]] \
    || die "$DEPLOY_CHECKOUT is not at the commit the bootstrap ran from"
  [[ "$(sudo stat -c '%a %U:%G' "$DEPLOY_CHECKOUT/README.md")" == *" $DEPLOY_USER:$DEPLOY_USER" ]] || die "$DEPLOY_CHECKOUT does not belong to $DEPLOY_USER"
  [[ "$(sudo stat -c '%a %U:%G' "$DEPLOY_COMPOSE_DIR/secrets")" == "700 $DEPLOY_USER:$DEPLOY_USER" ]] || die "the secrets folder is not private to $DEPLOY_USER"
  [[ "$(sudo stat -c '%a %U:%G' "$DEPLOY_COMPOSE_DIR/.env")" == "600 $DEPLOY_USER:$DEPLOY_USER" ]] || die ".env is not private to $DEPLOY_USER"
  sudo grep -qxF "ERP_SITE_ADDRESS=$SITE_ADDRESS" "$DEPLOY_COMPOSE_DIR/.env" || die ".env does not name ERP_SITE_ADDRESS=$SITE_ADDRESS"
  passed "$DEPLOY_CHECKOUT is at this commit and belongs to $DEPLOY_USER, with a private secrets folder and .env"
  local volume
  for volume in "${STACK_VOLUMES[@]}"; do
    sudo docker volume inspect "$volume" > /dev/null || die "the volume $volume is missing"
  done
  passed "the volumes ${STACK_VOLUMES[*]} exist"
}

prove_ipv4_ports() {
  log_step "The production stack publishes its ports on IPv4 only"
  local ports
  ports="$(cd infra/compose && ERP_SITE_ADDRESS="$SITE_ADDRESS" docker compose -f compose.yaml -f compose.production.yaml config --format json \
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
  require_command sudo ssh ssh-keygen jq git docker
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  WORK_DIR="$(mktemp -d)"
  trap remove_work_dir EXIT

  prepare_runner
  run_bootstrap
  prove_sign_in
  prove_access_rules
  prove_units_and_updates
  prove_deployment_folder
  prove_ipv4_ports
  printf 'host bootstrap test ok\n'
}

main "$@"
