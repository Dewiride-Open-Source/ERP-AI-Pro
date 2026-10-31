#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/heal.sh

Restarts each running container of the production stack that Docker reports unhealthy and that started at least 30
minutes ago. Docker restarts a container only when it exits, so a part of the ERP that stops answering without exiting
would otherwise stay down; one that started less than 30 minutes ago is left alone, so a container is restarted at most
every half hour. erp-ai-pro-heal.timer runs it every minute as the deploy account erp-ai-pro in the deployment checkout
/srv/erp-ai-pro/ERP-AI-Pro. It holds the lock /srv/erp-ai-pro/deploy.lock while it works and checks nothing in a minute
when a deployment, a rollback, a restore, the nightly retention run or backup or the stack'"'"'s start at boot holds it;
it never starts a stopped container. Each container it restarts or leaves alone is a line of its journal
(journalctl -u erp-ai-pro-heal.service). Runbook: docs/operations/runbooks/host-setup.md

Options:
  --help      Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/release.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/release.sh"

readonly STACK_PROJECT='erp-ai-pro'
readonly RESTART_INTERVAL_SECONDS=1800

heal() {
  local id="$1" service="$2" started since now minutes
  if ! started="$(host_docker inspect --format '{{.State.StartedAt}}' "$id")"; then
    log_error "cannot read when the $service container started"
    return 1
  fi
  if ! since="$(date -u -d "$started" +%s)"; then
    log_error "the $service container reports the start time '$(printable "$started")', which is not a time"
    return 1
  fi
  now="$(date -u +%s)"
  minutes=$(( (now - since) / 60 ))
  if (( now - since < RESTART_INTERVAL_SECONDS )); then
    log_info "the $service container is unhealthy, but it started $minutes minute(s) ago, so it is left alone until it has run 30 minutes"
    return 0
  fi
  if ! host_docker restart "$id" > /dev/null; then
    log_error "cannot restart the $service container, which Docker reports unhealthy"
    return 1
  fi
  log_warn "restarted the $service container, which Docker reported unhealthy after $minutes minute(s) of running"
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '$(printable "${ARGS[0]}")' (see --help)"
  require_command docker flock date
  require_release_host
  open_release_lock
  if ! flock --nonblock 9; then
    log_info "a deployment, a rollback, a restore, the nightly retention run or backup or the stack's start at boot holds $RELEASE_LOCK, so the stack is not checked this minute"
    exit 0
  fi
  local unhealthy id service failed=0
  unhealthy="$(host_docker ps --filter "label=com.docker.compose.project=$STACK_PROJECT" --filter health=unhealthy \
    --format '{{.ID}} {{.Label "com.docker.compose.service"}}')" || die "cannot list the containers of the $STACK_PROJECT stack"
  while read -r id service; do
    [[ -n "$id" ]] || continue
    heal "$id" "${service:-unnamed}" || failed=1
  done <<< "$unhealthy"
  exit "$failed"
}

main "$@"
