#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/deploy.sh <tag> [--dry-run]

Deploys the release <tag> of ERP-AI-Pro, such as v0.1.0, to this server. It runs as the deploy account erp-ai-pro in the
deployment checkout /srv/erp-ai-pro/ERP-AI-Pro and holds the lock /srv/erp-ai-pro/deploy.lock, which rollback.sh and the
systemd units share; it refuses to start in the half hour before a restart that an installed update waits for.

Before it changes anything it checks that the tag is a release cut from main and that each of the release'"'"'s three images
carries the provenance and the SBOM that the release workflow signed for that tag and its commit. Then it moves the checkout
to that commit and continues with the commit'"'"'s own deploy script, which checks the release again, pulls the images by
digest, asks the release'"'"'s migrator about the database (it stops when the database belongs to a newer release), applies
the migrations, writes the release into infra/compose/.env and the one it replaces into .env.previous, rolls the api, web
and proxy containers and waits until the API answers ready. A rollout that fails before any migration of the release was
applied goes back to the replaced release; after one, the server stays on the new release for a fix.
Runbook: docs/operations/runbooks/deploy.md

Options:
  --dry-run   Check the release and pull its images, changing neither .env nor the checkout.
  --help      Show this help.'

# shellcheck source=lib/release.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/release.sh"

TAG=''
VERSION=''
COMMIT=''
TARGET=''
CURRENT=''
PREVIOUS_BEFORE=''
ASSETS=''
RETURN_COMMIT=''
CONTINUED=0
MIGRATIONS_PENDING=0
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
  if [[ -n "$ASSETS" ]]; then
    rm -rf -- "$ASSETS"
  fi
  exit "$status"
}

read_continuation() {
  local assets="${ERP_DEPLOY_ASSETS:-}" return_commit="${ERP_DEPLOY_RETURN_COMMIT:-}"
  unset ERP_DEPLOY_ASSETS ERP_DEPLOY_RETURN_COMMIT
  if [[ "${ERP_DEPLOY_LOCK_PID:-}" != "$$" ]]; then
    return 0
  fi
  [[ -n "$assets" && -d "$assets" ]] || die "the deploy script this run continues left no release files behind"
  ASSETS="$assets"
  [[ "$return_commit" =~ $COMMIT_PATTERN ]] || die "the deploy script this run continues named no commit to go back to"
  RETURN_COMMIT="$return_commit"
  CONTINUED=1
}

read_release_files() {
  CURRENT="$(release_of_env_file "$ENV_FILE")" || die "fix $ENV_FILE first; nothing was changed"
  PREVIOUS_BEFORE="$(saved_release "$PREVIOUS_ENV_FILE")" \
    || die "fix or remove $PREVIOUS_ENV_FILE first, which only deploy.sh and rollback.sh write; nothing was changed"
  if [[ -z "$CURRENT" && -n "$PREVIOUS_BEFORE" ]]; then
    die "$PREVIOUS_ENV_FILE names the release v${PREVIOUS_BEFORE%% *}, but .env names none; remove .env.previous when no release ran here, or put the running release back into .env"
  fi
}

find_release() {
  log_step "Release $TAG"
  if (( ! CONTINUED )); then
    repo_git fetch --quiet --tags origin || die "cannot fetch main and the tags from origin; nothing was changed"
  fi
  COMMIT="$(release_commit "$TAG")" || die "origin has no tag $TAG, so no such release exists"
  require_on_main "$COMMIT" "$TAG"
  log_info "$TAG is commit $COMMIT on main"
}

verify_release() {
  if (( ! CONTINUED )); then
    ASSETS="$(mktemp -d -t erp-ai-pro-release.XXXXXXXX)" || die "cannot create a private folder for the release's files"
    log_step "Downloading the release files of $TAG"
    download_release_assets "$TAG" "$ASSETS" || die "nothing was changed"
  fi
  TARGET="$(release_of_file "$ASSETS/$RELEASE_IMAGES_ASSET")" \
    || die "$RELEASE_IMAGES_ASSET of $TAG is not exactly the four release values; nothing was changed"
  [[ "${TARGET%% *}" == "$VERSION" ]] \
    || die "$RELEASE_IMAGES_ASSET of $TAG names the version ${TARGET%% *}, not $VERSION; nothing was changed"
  log_step "Verifying the images of $TAG"
  verify_release_images "$TAG" "$COMMIT" "$ASSETS" "$TARGET" || die "the images of $TAG are not the ones its release workflow built; nothing was pulled or changed"
}

warn_about_host_changes() {
  local changed
  changed="$(repo_git diff --name-only "$1" "$COMMIT" -- infra/host scripts/deploy/bootstrap.sh scripts/deploy/lib/common.sh || true)"
  if [[ -n "$changed" ]]; then
    log_warn "$TAG changes what bootstrap.sh installs ($(paste -sd ' ' - <<< "$changed")): once it runs, the administrator runs the bootstrap again from a clone of $TAG (docs/operations/runbooks/host-setup.md, section Upkeep, Re-running the bootstrap)"
  fi
}

# The release's own script carries on, so a release that changes how it is deployed is deployed its own way.
continue_with_release_script() {
  local script="$REPO_ROOT/scripts/deploy/deploy.sh"
  repo_git cat-file -e "$COMMIT:scripts/deploy/deploy.sh" 2> /dev/null \
    || die "$TAG has no scripts/deploy/deploy.sh to continue with; nothing was changed"
  local head
  head="$(head_commit)" || die "cannot read the commit of the deployment checkout"
  [[ "$head" =~ $COMMIT_PATTERN ]] || die "the deployment checkout is at '$(printable "$head")', which is not a commit"
  RETURN_COMMIT="$head"
  warn_about_host_changes "$RETURN_COMMIT"
  log_step "Continuing with the deploy script of $TAG"
  move_checkout "$COMMIT" || die "cannot move the checkout to $COMMIT"
  export ERP_DEPLOY_ASSETS="$ASSETS" ERP_DEPLOY_RETURN_COMMIT="$RETURN_COMMIT"
  # Under errexit a failed exec ends the shell without its EXIT trap, which must move the checkout back.
  shopt -s execfail
  set +e
  exec bash -- "$script" "$@"
  set -e
  die "cannot start $script"
}

pull_release_images() {
  log_step "Pulling the images of $TAG by digest"
  write_release_file "$ASSETS/candidate.env" "$TARGET" || die "cannot stage the values of $TAG; nothing was changed"
  release_compose "$ASSETS/candidate.env" pull api web proxy || die "cannot pull the images of $TAG; nothing else was changed"
}

ask_migrator() {
  log_step "Asking the migrator of $TAG about the database"
  local status=0
  release_compose "$ASSETS/candidate.env" run --rm -T migrator status || status=$?
  case "$status" in
    0) log_info "the database holds every migration of $TAG, so none will be applied" ;;
    3)
      MIGRATIONS_PENDING=1
      log_info "migrations of $TAG will be applied"
      ;;
    4) die "the migrator of $TAG $(migrator_status_meaning 4), so $TAG is not deployed; deploy that release or a later one" ;;
    *) die "the migrator of $TAG $(migrator_status_meaning "$status") (see its output above); nothing was changed" ;;
  esac
}

report_running_release() {
  local status=0
  log_step "Asking the migrator of the running release v${CURRENT%% *} about the database"
  release_compose '' run --rm -T migrator status || status=$?
  case "$status" in
    0) log_info "the running release v${CURRENT%% *} finds the database as it expects" ;;
    4) log_error "the database now holds migrations of $TAG that the running release v${CURRENT%% *} does not know, the ones applied before the failure; fix forward with a new release or restore the database (docs/operations/runbooks/migrations.md, section Recovering from a bad migration)" ;;
    *) log_error "the migrator of the running release v${CURRENT%% *} $(migrator_status_meaning "$status")" ;;
  esac
}

migrate_database() {
  log_step "Applying the migrations of $TAG"
  local status=0
  release_compose "$ASSETS/candidate.env" run --rm -T migrator migrate || status=$?
  if (( status == 0 )); then
    return 0
  fi
  log_error "the migrator of $TAG failed with exit $status (see its output above); the running containers were not touched"
  if [[ -n "$RETURN_COMMIT" ]]; then
    if move_checkout "$RETURN_COMMIT"; then
      log_info "the checkout is back at ${RETURN_COMMIT:0:12}, the commit of the release .env names"
    else
      die "cannot move the checkout back to $RETURN_COMMIT; do it before the server restarts: git -C $REPO_ROOT checkout --detach $RETURN_COMMIT"
    fi
  fi
  if [[ -n "$CURRENT" ]]; then
    report_running_release
  fi
  die "$TAG was not deployed"
}

restore_previous_file() {
  if [[ -n "$PREVIOUS_BEFORE" ]]; then
    write_release_file "$PREVIOUS_ENV_FILE" "$PREVIOUS_BEFORE"
  else
    rm -f -- "$PREVIOUS_ENV_FILE"
  fi
}

record_release() {
  log_step "Writing $TAG into .env"
  if [[ "$CURRENT" == "$TARGET" ]]; then
    log_info ".env names $TAG already, so .env.previous keeps the release it names"
  else
    if [[ -n "$CURRENT" ]]; then
      write_release_file "$PREVIOUS_ENV_FILE" "$CURRENT" \
        || die "cannot keep the running release in $PREVIOUS_ENV_FILE; .env and the containers were not changed"
      log_info "kept the running release v${CURRENT%% *} in .env.previous for rollback.sh"
    fi
    if ! write_env_release "$ENV_FILE" "$TARGET"; then
      restore_previous_file || log_error "cannot put $PREVIOUS_ENV_FILE back as it was"
      die "cannot write $TAG into $ENV_FILE; the containers were not changed"
    fi
  fi
  ENV_WRITTEN=1
}

go_back() {
  local previous="$1" version="${1%% *}" commit
  commit="$(release_commit "v$version")" || die "$TAG did not become ready, and no tag v$version tells the commit to go back to, so the server stays on $TAG"
  move_checkout "$commit" || die "$TAG did not become ready, and the checkout cannot move to v$version, so the server stays on $TAG"
  if ! write_env_release "$ENV_FILE" "$previous"; then
    move_checkout "$COMMIT" || log_error "cannot move the checkout back to $COMMIT, the commit of $TAG, which .env names"
    die "$TAG did not become ready, and v$version cannot be written into .env, so the server stays on $TAG"
  fi
  restore_previous_file || log_error "cannot put $PREVIOUS_ENV_FILE back as it was before this run"
  if roll_out; then
    die "$TAG did not become ready (see the errors above), so the server went back to v$version, whose API answers $READINESS_STATE"
  fi
  die "$TAG did not become ready, and going back to v$version failed too; see docs/operations/runbooks/deploy.md, section When a rollout fails"
}

failed_rollout() {
  local previous status=0
  if (( MIGRATIONS_PENDING )); then
    die "$TAG did not become ready after its migrations were applied, which no earlier release knows, so the server stays on $TAG; fix forward with a new release or restore the database (docs/operations/runbooks/deploy.md, section When a rollout fails)"
  fi
  previous="$(saved_release "$PREVIOUS_ENV_FILE")" || die "$TAG did not become ready, and $PREVIOUS_ENV_FILE cannot be read, so the server stays on $TAG"
  if [[ -z "$previous" || "$previous" == "$TARGET" ]]; then
    die "$TAG did not become ready, and no other release ran here to go back to; its containers stay for diagnosis: docker compose -f compose.yaml -f compose.production.yaml logs api web proxy"
  fi
  log_step "Going back to v${previous%% *}"
  release_compose "$PREVIOUS_ENV_FILE" run --rm -T migrator status || status=$?
  if (( status != 0 )); then
    die "$TAG did not become ready, and the migrator of v${previous%% *} $(migrator_status_meaning "$status"), so the server stays on $TAG; see docs/operations/runbooks/deploy.md, section When a rollout fails"
  fi
  go_back "$previous"
}

succeeded() {
  local previous
  local -a keep=()
  mapfile -t keep < <(release_digests "$TARGET")
  previous="$(saved_release "$PREVIOUS_ENV_FILE" 2> /dev/null || true)"
  if [[ -n "$previous" ]]; then
    mapfile -t -O "${#keep[@]}" keep < <(release_digests "$previous")
  fi
  if [[ "$READINESS_STATE" == Degraded ]]; then
    log_warn "the API answers Degraded: it serves, but a dependency that only degrades it, App Configuration or the attachments storage, does not answer; see docker compose -f compose.yaml -f compose.production.yaml logs api"
  fi
  log_step "Removing the images of older releases"
  prune_release_images "${keep[@]}"
  printf 'deployed %s (commit %s): the API answers %s\n' "$TAG" "${COMMIT:0:12}" "$READINESS_STATE"
  exit 0
}

main() {
  local -a arguments=("$@")
  parse_args "$@"
  (( ${#ARGS[@]} == 1 )) || die "name the one release tag to deploy, such as v0.1.0 (see --help)"
  TAG="${ARGS[0]}"
  VERSION="$(release_version_of_tag "$TAG")" || die "'$(printable "$TAG")' is not a release tag such as v0.1.0"
  require_command git docker gh curl flock mktemp
  require_release_host
  trap finish EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  read_continuation
  acquire_release_lock
  refuse_before_the_nightly_restart
  read_release_files
  require_gh_version || die "nothing was changed"
  find_release
  require_clean_checkout
  verify_release

  local head
  head="$(head_commit)" || die "cannot read the commit of the deployment checkout"
  if [[ "$head" != "$COMMIT" ]]; then
    if (( CONTINUED )); then
      die "the deploy script of $TAG runs, but the checkout is at ${head:0:12}, not at $TAG's ${COMMIT:0:12}"
    elif (( DRY_RUN )); then
      warn_about_host_changes "$head"
      log_info "a deployment moves the checkout from ${head:0:12} to ${COMMIT:0:12} and continues with the deploy script of $TAG; this dry run pulls with the compose files of ${head:0:12}"
    else
      continue_with_release_script "${arguments[@]}"
    fi
  fi

  pull_release_images
  if (( DRY_RUN )); then
    printf 'dry run: %s (commit %s) is verified and its images are pulled; .env and the checkout are unchanged\n' "$TAG" "${COMMIT:0:12}"
    exit 0
  fi
  ask_migrator
  migrate_database
  record_release
  log_step "Rolling the api, web and proxy containers to $TAG"
  if roll_out; then
    succeeded
  fi
  failed_rollout
}

main "$@"
