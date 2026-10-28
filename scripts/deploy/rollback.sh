#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/rollback.sh [--dry-run]

Puts this server back on the release that ran before the current one: the release infra/compose/.env.previous names, which
deploy.sh keeps. It runs as the deploy account erp-ai-pro in the deployment checkout /srv/erp-ai-pro/ERP-AI-Pro and holds
the lock that deploy.sh and the systemd units share; it refuses to start in the half hour before a restart that an
installed update waits for.

It goes back only when that release'"'"'s images are still on this server and its migrator finds every migration it knows
applied and no other, because an older API against a newer database is not supported. Then it moves the checkout to the
release'"'"'s commit, swaps .env and .env.previous, rolls the api, web and proxy containers and waits until the API answers
ready. Running it again goes forward to the release it left. Runbook: docs/operations/runbooks/deploy.md

Options:
  --dry-run   Check that the rollback can run, changing neither .env nor the checkout.
  --help      Show this help.'

# shellcheck source=lib/release.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/release.sh"

CURRENT=''
PREVIOUS=''
RETURN_COMMIT=''
ENV_WRITTEN=0

# Until .env names the release, the checkout must stay at the commit .env runs, which a start at boot reads its compose files from.
finish() {
  local status=$?
  if (( status != 0 && ! ENV_WRITTEN )) && [[ -n "$RETURN_COMMIT" && "$(head_commit 2> /dev/null || true)" != "$RETURN_COMMIT" ]]; then
    if move_checkout "$RETURN_COMMIT"; then
      log_info "the checkout is back at ${RETURN_COMMIT:0:12}, the commit of the release .env names"
    else
      log_error "cannot move the checkout back to $RETURN_COMMIT; do it before the server restarts: git -C $REPO_ROOT checkout --detach $RETURN_COMMIT"
    fi
  fi
  exit "$status"
}

read_releases() {
  CURRENT="$(release_of_env_file "$ENV_FILE")" || die "fix $ENV_FILE first; nothing was changed"
  [[ -n "$CURRENT" ]] || die ".env names no release, so no release runs here to go back from"
  PREVIOUS="$(saved_release "$PREVIOUS_ENV_FILE")" \
    || die "fix or remove $PREVIOUS_ENV_FILE first, which only deploy.sh and rollback.sh write; nothing was changed"
  [[ -n "$PREVIOUS" ]] || die "no earlier release is kept here ($PREVIOUS_ENV_FILE is missing); deploy one with deploy.sh instead"
  [[ "$PREVIOUS" != "$CURRENT" ]] \
    || die ".env.previous names the running release v${CURRENT%% *}, so there is no other release to go back to"
}

ask_previous_migrator() {
  local version="$1" status=0
  log_step "Asking the migrator of v$version about the database"
  release_compose "$PREVIOUS_ENV_FILE" run --rm -T migrator status || status=$?
  case "$status" in
    0) log_info "the migrator of v$version $(migrator_status_meaning 0)" ;;
    3) die "the migrator of v$version $(migrator_status_meaning 3), which a rollback never applies; deploy v$version with deploy.sh instead" ;;
    4) die "the migrator of v$version $(migrator_status_meaning 4), so the server stays on v${CURRENT%% *}; fix forward with a new release or restore the database (docs/operations/runbooks/deploy.md)" ;;
    *) die "the migrator of v$version $(migrator_status_meaning "$status") (see its output above); nothing was changed" ;;
  esac
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '$(printable "${ARGS[0]}")' (see --help)"
  require_command git docker flock
  require_release_host
  trap finish EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  acquire_release_lock
  refuse_before_the_nightly_restart
  read_releases

  local version="${PREVIOUS%% *}" commit head
  local -a missing=()
  log_step "Going back from v${CURRENT%% *} to v$version"
  commit="$(release_commit "v$version")" || die "this checkout knows no tag v$version, so the commit to go back to is unknown; nothing was changed"
  require_on_main "$commit" "v$version"
  require_clean_checkout
  mapfile -t missing < <(missing_release_images "$PREVIOUS")
  (( ${#missing[@]} == 0 )) \
    || die "the ${missing[*]} image(s) of v$version are no longer on this server; deploy v$version with deploy.sh instead, which checks and pulls them"
  ask_previous_migrator "$version"
  if (( DRY_RUN )); then
    printf 'dry run: the server can go back to v%s (commit %s); .env and the checkout are unchanged\n' "$version" "${commit:0:12}"
    exit 0
  fi

  head="$(head_commit)" || die "cannot read the commit of the deployment checkout"
  [[ "$head" =~ $COMMIT_PATTERN ]] || die "the deployment checkout is at '$(printable "$head")', which is not a commit"
  RETURN_COMMIT="$head"
  move_checkout "$commit" || die "cannot move the checkout to $commit, the commit of v$version"
  write_env_release "$ENV_FILE" "$PREVIOUS" || die "cannot write v$version into $ENV_FILE; the containers were not changed"
  ENV_WRITTEN=1
  if write_release_file "$PREVIOUS_ENV_FILE" "$CURRENT"; then
    log_info ".env names v$version, and .env.previous v${CURRENT%% *}"
  else
    log_error "cannot keep v${CURRENT%% *} in $PREVIOUS_ENV_FILE; to go forward again later, run deploy.sh v${CURRENT%% *}"
  fi

  log_step "Rolling the api, web and proxy containers back to v$version"
  if ! roll_out; then
    die "v$version did not become ready either (see the errors above); run deploy.sh v${CURRENT%% *} to go forward again, or see docs/operations/runbooks/deploy.md, section When a rollout fails"
  fi
  if [[ "$READINESS_STATE" == Degraded ]]; then
    log_warn "the API answers Degraded: it serves, but a dependency that only degrades it, App Configuration or the attachments storage, does not answer; see docker compose -f compose.yaml -f compose.production.yaml logs api"
  fi
  printf 'rolled back to v%s (commit %s): the API answers %s\n' "$version" "${commit:0:12}" "$READINESS_STATE"
}

main "$@"
