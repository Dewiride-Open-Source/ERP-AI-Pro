#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/tests/release-decisions.sh

Runs deploy.sh and rollback.sh, and the verify_release_image function the release workflow calls, in a temporary folder
that stands for the server'"'"'s root (ERP_DEPLOY_TEST_ROOT names it to them), with tests/stand-in.sh on PATH as docker,
gh, curl, git, flock, wget, id, date, sleep and mv, recording every call. It checks each decision the scripts make:
  - a deployment checks the release before anything else, continues with the release'"'"'s own deploy script, which checks
    it again from the files already downloaded, then pulls, asks the migrator, migrates, writes .env and .env.previous,
    rolls out, reads the API'"'"'s readiness and removes the images of older releases, in that order;
  - a malformed tag or release file, a refused bundle, a bundle of another repository, a commit off main, a gh older than
    2.102.0, local changes in the checkout, a lock held past the wait, another account, a copy outside the deployment
    checkout, another root and the half hour before a restart stop it before it changes anything, and a lock released
    while it waits lets it run;
  - a failed migration rolls nothing and puts the checkout back, and so does a release'"'"'s own deploy script that stops at
    a check of its own; a .env.previous that cannot be written after the release'"'"'s migrations rolls nothing and says so;
    a failed rollout goes back to the replaced release only when no migration of the new one was applied and the
    replaced release'"'"'s migrator agrees, and a failed rollout of the release .env names already stays on it;
  - a Degraded API passes with a warning; a database of a newer release stops a deployment and a rollback; deploying the
    release .env names keeps .env.previous; the first deployment adds the digest lines .env lacks; a dry run changes
    neither .env nor the checkout; a rollback goes back only when the replaced release'"'"'s migrator finds every migration
    it knows applied and no other, and swaps the two.
It needs bash with the usual command-line tools (coreutils, grep, sed, find) and nothing else, so it runs on any machine
with them, Git Bash included; check.sh runs it.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=../lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/../lib/common.sh"

readonly STAND_IN_COMMANDS=(docker gh curl git flock wget id date sleep mv)
readonly IMAGES=(api web proxy)
readonly VERSIONS=(0.0.8 0.0.9 0.1.0 0.2.0)
readonly COMMIT_V009='0909090909090909090909090909090909090909'
readonly COMMIT_V010='1010101010101010101010101010101010101010'
readonly COMMIT_V020='2020202020202020202020202020202020202020'
readonly COMMIT_V030='3030303030303030303030303030303030303030'
readonly REGISTRY='ghcr.io/dewiride-open-source/erp-ai-pro'
readonly COMPOSE='docker compose -f compose.yaml -f compose.production.yaml --env-file .env'
readonly HOST_PATHS=(infra/host scripts/deploy/bootstrap.sh scripts/deploy/lib/common.sh)
readonly TARGET_MARKER='printf '\''target deploy.sh %s\n'\'' "$*" >> "$RELEASE_DECISIONS_CALLS"'
readonly TREE_FILES=(scripts/deploy/deploy.sh scripts/deploy/rollback.sh scripts/deploy/lib/common.sh scripts/deploy/lib/release.sh
  infra/host/apt/52erp-ai-pro-unattended-upgrades.conf)

declare -A DIGESTS=()
WORK=''
ROOT=''
CHECKOUT=''
COMPOSE_FOLDER=''
BIN=''
STATE=''
SCENARIO=''
STATUS=0
OUTPUT=''
TEXT=''
PASSED=0

remove_work() {
  if [[ -n "$WORK" ]]; then
    rm -rf -- "$WORK"
  fi
}

fail() {
  log_error "$SCENARIO: $*"
  printf -- '--- output\n%s\n--- calls\n' "$OUTPUT" >&2
  cat -- "$STATE/calls" >&2 || true
  exit 1
}

make_digests() {
  local image version unit text index
  for image in "${IMAGES[@]}"; do
    for version in "${VERSIONS[@]}"; do
      case "$image" in
        api) unit=a ;;
        web) unit=b ;;
        proxy) unit=c ;;
      esac
      unit="$unit${version//./}"
      text=''
      for (( index = 0; index < 16; index++ )); do
        text+="$unit"
      done
      DIGESTS["$image $version"]="sha256:$text"
    done
  done
}

release_text() {
  printf -v TEXT 'IMAGE_TAG=%s\nAPI_IMAGE_DIGEST=%s\nWEB_IMAGE_DIGEST=%s\nPROXY_IMAGE_DIGEST=%s' \
    "$1" "${DIGESTS[api $1]}" "${DIGESTS[web $1]}" "${DIGESTS[proxy $1]}"
}

env_text() {
  printf -v TEXT '# Stand-in production values\nERP_SITE_ADDRESS=192.0.2.10\nIMAGE_TAG=%s\nAPI_IMAGE_DIGEST=%s\nWEB_IMAGE_DIGEST=%s\nPROXY_IMAGE_DIGEST=%s\nAPPCONFIG_ENDPOINT=https://appcs-stand-in.azconfig.io' \
    "$1" "$2" "$3" "$4"
}

release_env_text() {
  env_text "$1" "${DIGESTS[api $1]}" "${DIGESTS[web $1]}" "${DIGESTS[proxy $1]}"
}

write_text() {
  printf '%s\n' "$TEXT" > "$1"
}

verify_call() {
  local predicate=https://spdx.dev/Document/v2.3
  if [[ "$2" == provenance ]]; then
    predicate=https://slsa.dev/provenance/v1
  fi
  printf -v TEXT 'gh attestation verify oci://%s/%s@%s --repo Dewiride-Open-Source/ERP-AI-Pro --bundle %s.%s.sigstore.json --signer-workflow github.com/Dewiride-Open-Source/ERP-AI-Pro/.github/workflows/release.yml@refs/tags/v%s --source-ref refs/tags/v%s --source-digest %s --deny-self-hosted-runners --predicate-type %s --format json --jq .[].verificationResult.signature.certificate.sourceRepositoryIdentifier' \
    "$REGISTRY" "$1" "${DIGESTS[$1 $3]}" "$1" "$2" "$3" "$3" "$4" "$predicate"
}

curl_call() {
  printf -v TEXT 'curl -fsSL --retry 3 --proto =https --proto-redir =https --max-time 300 --max-filesize 67108864 --output %s https://github.com/Dewiride-Open-Source/ERP-AI-Pro/releases/download/%s/%s' \
    "$2" "$1" "$2"
}

prepare_work() {
  WORK="$(cd -- "$(mktemp -d)" && pwd -P)"
  trap remove_work EXIT
  ROOT="$WORK/root"
  CHECKOUT="$ROOT$DEPLOY_CHECKOUT"
  COMPOSE_FOLDER="$CHECKOUT/infra/compose"
  BIN="$WORK/bin"
  STATE="$WORK/state"
  mkdir -p -- "$BIN" "$WORK/trees"
  local command commit file
  for command in "${STAND_IN_COMMANDS[@]}"; do
    cp -- "$SCRIPT_DIR/tests/stand-in.sh" "$BIN/$command"
    chmod +x -- "$BIN/$command"
  done
  for commit in "$COMMIT_V009" "$COMMIT_V010" "$COMMIT_V020"; do
    for file in "${TREE_FILES[@]}"; do
      mkdir -p -- "$WORK/trees/$commit/${file%/*}"
      cp -- "$REPO_ROOT/$file" "$WORK/trees/$commit/$file"
    done
  done
  {
    IFS= read -r file
    printf '%s\n%s\n' "$file" "$TARGET_MARKER"
    cat
  } < "$REPO_ROOT/scripts/deploy/deploy.sh" > "$WORK/trees/$COMMIT_V020/scripts/deploy/deploy.sh"
  make_digests
}

new_world() {
  SCENARIO="$1"
  OUTPUT=''
  rm -rf -- "$ROOT" "$STATE" "$WORK/tmp"
  mkdir -p -- "$COMPOSE_FOLDER" "$ROOT/var/run" "$WORK/tmp" "$STATE/git/tags" "$STATE/docker/images" "$STATE/release/v0.2.0"
  cp -R -- "$WORK/trees" "$STATE/git/trees"
  cp -R -- "$WORK/trees/$COMMIT_V010/." "$CHECKOUT/"
  printf '%s\n' "$COMMIT_V010" > "$STATE/git/head"
  printf '%s\n' "$COMMIT_V009" > "$STATE/git/tags/v0.0.9"
  printf '%s\n' "$COMMIT_V010" > "$STATE/git/tags/v0.1.0"
  printf '%s\n' "$COMMIT_V020" > "$STATE/git/tags/v0.2.0"
  printf '%s\n' "$COMMIT_V030" > "$STATE/git/tags/v0.3.0"
  printf '%s\n' "$COMMIT_V009" "$COMMIT_V010" "$COMMIT_V020" > "$STATE/git/main"
  printf 'infra/host/systemd/erp-ai-pro-retain.service\n' > "$STATE/git/host-changes"
  release_env_text 0.1.0
  write_text "$COMPOSE_FOLDER/.env"
  release_text 0.0.9
  write_text "$COMPOSE_FOLDER/.env.previous"
  release_text 0.2.0
  write_text "$STATE/release/v0.2.0/release-images.env"
  local image kind version
  for image in "${IMAGES[@]}"; do
    for kind in provenance sbom; do
      printf '%s %s v0.2.0\n' "$image" "$kind" > "$STATE/release/v0.2.0/$image.$kind.sigstore.json"
    done
    for version in "${VERSIONS[@]}"; do
      printf '%s\n' "${DIGESTS[$image $version]}"
    done > "$STATE/docker/images/$image"
  done
  : > "$STATE/calls"
}

state() {
  mkdir -p -- "$(dirname -- "$STATE/$1")"
  printf '%s\n' "$2" > "$STATE/$1"
}

move_head_to() {
  printf '%s\n' "$1" > "$STATE/git/head"
  cp -R -- "$WORK/trees/$1/." "$CHECKOUT/"
}

run_script() {
  run_script_from "$CHECKOUT" "$ROOT" "$@"
}

run_script_from() {
  local folder="$1" root="$2" script="$3"
  shift 3
  : > "$STATE/calls"
  STATUS=0
  OUTPUT="$(cd -- "$WORK" && env -u DRY_RUN -u ERP_DEPLOY_LOCK_PID -u ERP_DEPLOY_ASSETS -u ERP_DEPLOY_RETURN_COMMIT \
    PATH="$BIN:$PATH" ERP_DEPLOY_TEST_ROOT="$root" TMPDIR="$WORK/tmp" RELEASE_DECISIONS_CALLS="$STATE/calls" \
    bash "$folder/scripts/deploy/$script" "$@" 2>&1)" || STATUS=$?
}

expect_status() {
  [[ "$STATUS" == "$1" ]] || fail "exited $STATUS, not $1"
}

expect_output() {
  [[ "$OUTPUT" == *"$1"* ]] || fail "printed nothing like: $1"
}

expect_calls() {
  local expected index=0
  local -a calls=()
  mapfile -t calls < "$STATE/calls"
  for expected in "$@"; do
    while (( index < ${#calls[@]} )) && [[ "${calls[$index]}" != "$expected" ]]; do
      index=$(( index + 1 ))
    done
    (( index < ${#calls[@]} )) || fail "made no call '$expected' after the calls before it"
    index=$(( index + 1 ))
  done
}

expect_only_calls() {
  local -a calls=()
  mapfile -t calls < "$STATE/calls"
  [[ "${calls[*]}" == "$*" ]] || fail "made the calls '${calls[*]}', not '$*'"
}

expect_no_call() {
  if grep -qF -- "$1" "$STATE/calls"; then
    fail "made a call like '$1'"
  fi
}

expect_call_count() {
  local count
  count="$(grep -cF -- "$2" "$STATE/calls" || true)"
  [[ "$count" == "$1" ]] || fail "made $count call(s) like '$2', not $1"
}

expect_file() {
  [[ -f "$1" ]] || fail "$1 is missing"
  [[ "$(< "$1")" == "$2" ]] || fail "$1 holds: $(< "$1")"
}

expect_missing() {
  [[ ! -e "$1" ]] || fail "$1 exists"
}

expect_head() {
  [[ "$(< "$STATE/git/head")" == "$1" ]] || fail "the checkout is at $(< "$STATE/git/head"), not $1"
}

expect_unchanged() {
  release_env_text 0.1.0
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  release_text 0.0.9
  expect_file "$COMPOSE_FOLDER/.env.previous" "$TEXT"
  expect_head "$COMMIT_V010"
}

passed() {
  expect_no_call UNEXPECTED
  [[ -z "$(ls -A -- "$WORK/tmp")" ]] || fail "left $(ls -A -- "$WORK/tmp") in the temporary folder"
  if compgen -G "$COMPOSE_FOLDER/.*.staged.*" > /dev/null; then
    fail "left a staged file in $COMPOSE_FOLDER"
  fi
  PASSED=$(( PASSED + 1 ))
  log_info "ok: $SCENARIO"
}

release_checks() {
  local tag="$1" commit="$2" image kind
  local -a checks=()
  for image in "${IMAGES[@]}"; do
    for kind in provenance sbom; do
      verify_call "$image" "$kind" "${tag#v}" "$commit"
      checks+=("$TEXT")
    done
  done
  CHECKS=("${checks[@]}")
}

CHECKS=()

scenario_deployment_order() {
  new_world "a deployment checks the release, continues with its own deploy script and rolls out in order"
  local -a downloads=()
  local asset
  for asset in release-images.env api.provenance.sigstore.json api.sbom.sigstore.json web.provenance.sigstore.json \
    web.sbom.sigstore.json proxy.provenance.sigstore.json proxy.sbom.sigstore.json; do
    curl_call v0.2.0 "$asset"
    downloads+=("$TEXT")
  done
  release_checks v0.2.0 "$COMMIT_V020"
  run_script deploy.sh v0.2.0
  expect_status 0
  expect_calls \
    "id -un" "flock --nonblock 9" "gh --version" "git fetch --quiet --tags origin" \
    "git rev-parse --verify --quiet refs/tags/v0.2.0^{commit}" \
    "git merge-base --is-ancestor $COMMIT_V020 refs/remotes/origin/main" \
    "git status --porcelain --untracked-files=no" \
    "${downloads[@]}" "${CHECKS[@]}" \
    "git rev-parse --verify HEAD" \
    "git cat-file -e $COMMIT_V020:scripts/deploy/deploy.sh" \
    "git diff --name-only $COMMIT_V010 $COMMIT_V020 -- ${HOST_PATHS[*]}" \
    "git checkout --quiet --detach $COMMIT_V020" \
    "target deploy.sh v0.2.0" \
    "id -un" "flock --nonblock 9" "gh --version" \
    "git rev-parse --verify --quiet refs/tags/v0.2.0^{commit}" \
    "git merge-base --is-ancestor $COMMIT_V020 refs/remotes/origin/main" \
    "git status --porcelain --untracked-files=no" \
    "${CHECKS[@]}" \
    "git rev-parse --verify HEAD" \
    "$COMPOSE --env-file candidate.env pull api web proxy # 0.2.0" \
    "$COMPOSE --env-file candidate.env run --rm -T migrator status # 0.2.0" \
    "$COMPOSE --env-file candidate.env run --rm -T migrator migrate # 0.2.0" \
    "$COMPOSE up --detach --wait --wait-timeout 300 --no-deps --no-build --pull never api web proxy # 0.2.0" \
    "$COMPOSE exec -T proxy wget -qO- -T 5 http://api:8080/healthz/ready # 0.2.0" \
    "docker image ls --digests --format {{.Digest}} $REGISTRY/api" \
    "docker image rm $REGISTRY/api@${DIGESTS[api 0.0.8]}" \
    "docker image rm $REGISTRY/api@${DIGESTS[api 0.0.9]}" \
    "docker image ls --digests --format {{.Digest}} $REGISTRY/web" \
    "docker image rm $REGISTRY/web@${DIGESTS[web 0.0.8]}" \
    "docker image rm $REGISTRY/web@${DIGESTS[web 0.0.9]}" \
    "docker image ls --digests --format {{.Digest}} $REGISTRY/proxy" \
    "docker image rm $REGISTRY/proxy@${DIGESTS[proxy 0.0.8]}" \
    "docker image rm $REGISTRY/proxy@${DIGESTS[proxy 0.0.9]}"
  expect_call_count 1 "git fetch "
  expect_call_count 7 "curl "
  expect_call_count 12 "gh attestation verify "
  expect_call_count 1 "target deploy.sh "
  expect_call_count 1 " up --detach "
  expect_call_count 6 "docker image rm "
  expect_no_call "wget -qO- -T 5 http://api:8080/healthz/ready # 0.1.0"
  expect_head "$COMMIT_V020"
  release_env_text 0.2.0
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  release_text 0.1.0
  expect_file "$COMPOSE_FOLDER/.env.previous" "$TEXT"
  expect_output "[warn] v0.2.0 changes what bootstrap.sh installs (infra/host/systemd/erp-ai-pro-retain.service)"
  expect_output "deployed v0.2.0 (commit 202020202020): the API answers Healthy"
  passed
}

scenario_target_check_refuses() {
  new_world "a release's own deploy script that stops at a check of its own puts the checkout back"
  local target="$STATE/git/trees/$COMMIT_V020/scripts/deploy/deploy.sh"
  sed -i 's/^  require_command git docker gh curl flock mktemp$/& erp-missing-command/' "$target"
  grep -qF -- 'mktemp erp-missing-command' "$target" || fail "the release's deploy script has no require_command line to extend"
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "required command 'erp-missing-command' is not on PATH"
  expect_calls "git checkout --quiet --detach $COMMIT_V020" "target deploy.sh v0.2.0" "git checkout --quiet --detach $COMMIT_V010"
  expect_no_call "docker "
  expect_unchanged
  passed
}

scenario_malformed_tags() {
  new_world "a malformed tag reaches no command"
  local tag
  for tag in v1.2 1.2.3 v01.2.3 V1.2.3 v1.2.3-rc.1 'v1.2.3;id' '../v1.2.3' 'v1.2.3/../../x' 'v1.2.3 ' '' \
    "v1.2.$(printf '9%.0s' {1..40})"; do
    run_script deploy.sh "$tag"
    expect_status 1
    expect_output "is not a release tag such as v0.1.0"
    expect_only_calls
  done
  run_script deploy.sh v0.2.0 v0.1.0
  expect_status 1
  expect_only_calls
  expect_unchanged
  passed
}

scenario_malformed_release_files() {
  new_world "a release file that is not exactly the four values stops the deployment before any check"
  local asset="$STATE/release/v0.2.0/release-images.env" variant
  for variant in extra crlf version digest duplicate missing; do
    release_text 0.2.0
    case "$variant" in
      extra) TEXT+=$'\nAPPCONFIG_ENDPOINT=https://elsewhere.example' ;;
      crlf) TEXT="${TEXT//$'\n'/$'\r\n'}" ;;
      version) TEXT="${TEXT/IMAGE_TAG=0.2.0/IMAGE_TAG=0.2.1}" ;;
      digest) TEXT="${TEXT/${DIGESTS[web 0.2.0]}/sha256:B020}" ;;
      duplicate) TEXT="${TEXT/PROXY_IMAGE_DIGEST=/IMAGE_TAG=0.2.0$'\n'PROXY_IMAGE_DIGEST=}" ;;
      missing) TEXT="${TEXT%$'\n'*}" ;;
    esac
    write_text "$asset"
    run_script deploy.sh v0.2.0
    expect_status 1
    expect_call_count 0 "gh attestation verify "
    expect_no_call "docker "
    expect_no_call "git checkout "
  done
  expect_unchanged
  passed
}

scenario_refused_bundle() {
  new_world "a bundle gh refuses stops the deployment before anything is pulled"
  state gh/web.sbom refuse
  run_script deploy.sh v0.2.0
  expect_status 1
  verify_call web sbom 0.2.0 "$COMMIT_V020"
  expect_calls "$TEXT"
  expect_output "gh refused the SBOM of the web image ${DIGESTS[web 0.2.0]} for v0.2.0"
  expect_no_call "docker "
  expect_no_call "git checkout "
  expect_unchanged
  passed
}

scenario_other_repository() {
  new_world "a bundle signed in another repository stops the deployment before anything is pulled"
  state gh/proxy.provenance repository:987654321
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "signed in the repository with the id '987654321', not in Dewiride-Open-Source/ERP-AI-Pro (1377214678)"
  expect_no_call "docker "
  expect_no_call "git checkout "
  expect_unchanged
  passed
}

scenario_commit_off_main() {
  new_world "a tag whose commit is not on main is not downloaded"
  run_script deploy.sh v0.3.0
  expect_status 1
  expect_output "v0.3.0 (commit 303030303030) is not on main"
  expect_no_call "curl "
  expect_no_call "docker "
  expect_unchanged
  passed
}

scenario_old_gh() {
  new_world "a gh older than 2.102.0 stops the deployment before it fetches"
  state gh/version 2.101.9
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "gh 2.101.9 is older than 2.102.0"
  expect_no_call "git fetch "
  expect_no_call "curl "
  expect_unchanged
  passed
}

scenario_local_changes() {
  new_world "local changes in the checkout stop the deployment before it downloads"
  state git/dirty ' M infra/compose/compose.production.yaml'
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "the deployment checkout has local changes, which no release carries"
  expect_no_call "curl "
  expect_unchanged
  passed
}

scenario_lock_held() {
  new_world "a lock held past the wait stops a deployment and a rollback"
  state flock-held yes
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "still holds $ROOT/srv/erp-ai-pro/deploy.lock after 14 minutes"
  expect_only_calls "id -un" "flock --nonblock 9" "flock --wait 840 9"
  run_script rollback.sh
  expect_status 1
  expect_output "still holds $ROOT/srv/erp-ai-pro/deploy.lock after 14 minutes"
  expect_only_calls "id -un" "flock --nonblock 9" "flock --wait 840 9"
  expect_unchanged
  passed
}

scenario_lock_released_while_waiting() {
  new_world "a lock released while a deployment or a rollback waits lets it run"
  state flock-held yes
  state flock-released yes
  run_script deploy.sh --dry-run v0.2.0
  expect_status 0
  expect_calls "id -un" "flock --nonblock 9" "flock --wait 840 9" "gh --version" \
    "$COMPOSE --env-file candidate.env pull api web proxy # 0.2.0"
  expect_output "waiting up to 14 minutes for it"
  expect_output "dry run: v0.2.0 (commit 202020202020) is verified and its images are pulled"
  run_script rollback.sh --dry-run
  expect_status 0
  expect_calls "id -un" "flock --nonblock 9" "flock --wait 840 9" \
    "$COMPOSE --env-file .env.previous run --rm -T migrator status # 0.0.9"
  expect_output "dry run: the server can go back to v0.0.9 (commit 090909090909)"
  expect_unchanged
  passed
}

scenario_other_account() {
  new_world "another account stops a deployment and a rollback at once"
  state account runner
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "run this as the deploy account erp-ai-pro, not as runner"
  expect_only_calls "id -un"
  run_script rollback.sh
  expect_status 1
  expect_only_calls "id -un"
  expect_unchanged
  passed
}

scenario_other_folder() {
  new_world "a copy outside the deployment checkout, or another root, stops a deployment and a rollback at once"
  local clone="$ROOT/home/erp-ai-pro/ERP-AI-Pro" other="$ROOT/tmp/other-root"
  mkdir -p -- "$clone" "$other$DEPLOY_HOME"
  cp -R -- "$CHECKOUT/." "$clone/"
  run_script_from "$clone" "$ROOT" deploy.sh v0.2.0
  expect_status 1
  expect_output "run the copy of this script in the deployment checkout $CHECKOUT, which deployments move; this one is in $clone"
  expect_only_calls "id -un"
  run_script_from "$clone" "$ROOT" rollback.sh
  expect_status 1
  expect_output "this one is in $clone"
  expect_only_calls "id -un"
  run_script_from "$CHECKOUT" "$other" deploy.sh v0.2.0
  expect_status 1
  expect_output "run the copy of this script in the deployment checkout $other$DEPLOY_CHECKOUT, which deployments move; this one is in $CHECKOUT"
  expect_only_calls "id -un"
  run_script_from "$CHECKOUT" "$other" rollback.sh
  expect_status 1
  expect_output "this one is in $CHECKOUT"
  expect_only_calls "id -un"
  expect_missing "$ROOT$DEPLOY_LOCK"
  expect_missing "$other$DEPLOY_LOCK"
  expect_unchanged
  passed
}

scenario_restart_window() {
  new_world "a restart an update waits for stops a run in the half hour before it"
  : > "$ROOT/var/run/reboot-required"
  local clock
  for clock in 2100 2130; do
    state clock "$clock"
    run_script deploy.sh v0.2.0
    expect_status 1
    expect_output "waits for the server's restart at 21:30 UTC (03:00 India time)"
    expect_only_calls "id -un" "flock --nonblock 9" "date +%H%M"
    run_script rollback.sh
    expect_status 1
    expect_only_calls "id -un" "flock --nonblock 9" "date +%H%M"
  done
  for clock in 2059 2131; do
    state clock "$clock"
    run_script deploy.sh v0.3.0
    expect_status 1
    expect_output "is not on main"
  done
  rm -f -- "$ROOT/var/run/reboot-required"
  state clock 2110
  run_script deploy.sh v0.3.0
  expect_output "is not on main"
  expect_no_call "date "
  expect_unchanged
  passed
}

scenario_failed_migration() {
  new_world "a failed migration rolls nothing, puts the checkout back and asks the running release's migrator"
  state docker/0.2.0/migrate-exit 1
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_calls \
    "$COMPOSE --env-file candidate.env run --rm -T migrator migrate # 0.2.0" \
    "git checkout --quiet --detach $COMMIT_V010" \
    "$COMPOSE run --rm -T migrator status # 0.1.0"
  expect_no_call " up --detach "
  expect_output "v0.2.0 was not deployed"
  expect_unchanged
  passed
}

scenario_record_failure_after_migrations() {
  new_world "a .env.previous that cannot be written after the release's migrations rolls nothing and says the database moved"
  state docker/0.2.0/status-exit 3
  state docker/0.1.0/status-exit 4
  state mv-refuses /.env.previous
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_calls \
    "$COMPOSE --env-file candidate.env run --rm -T migrator migrate # 0.2.0" \
    "mv refused $COMPOSE_FOLDER/.env.previous" \
    "git checkout --quiet --detach $COMMIT_V010" \
    "$COMPOSE run --rm -T migrator status # 0.1.0"
  expect_no_call " up --detach "
  expect_output "the database now holds migrations of v0.2.0 that the running release v0.1.0 does not know"
  expect_output "v0.2.0 was not deployed, but its migrations were applied, which the running release may not know"
  expect_unchanged
  passed
}

scenario_failed_rollout_goes_back() {
  new_world "a failed rollout with no migration applied goes back to the replaced release"
  state docker/0.2.0/up-exit 1
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_calls \
    "$COMPOSE up --detach --wait --wait-timeout 300 --no-deps --no-build --pull never api web proxy # 0.2.0" \
    "$COMPOSE --env-file .env.previous run --rm -T migrator status # 0.1.0" \
    "git rev-parse --verify --quiet refs/tags/v0.1.0^{commit}" \
    "git checkout --quiet --detach $COMMIT_V010" \
    "$COMPOSE up --detach --wait --wait-timeout 300 --no-deps --no-build --pull never api web proxy # 0.1.0" \
    "$COMPOSE exec -T proxy wget -qO- -T 5 http://api:8080/healthz/ready # 0.1.0"
  expect_output "v0.2.0 did not become ready (see the errors above), so the server went back to v0.1.0, whose API answers Healthy"
  expect_no_call "docker image rm "
  expect_unchanged
  passed
}

scenario_failed_rollout_after_migrations() {
  new_world "a failed rollout after the release's migrations stays on the release"
  state docker/0.2.0/status-exit 3
  state docker/0.2.0/ready-exit 1
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_call_count 1 " up --detach "
  expect_call_count 24 "exec -T proxy wget -qO- -T 5 http://api:8080/healthz/ready # 0.2.0"
  expect_call_count 23 "sleep 5"
  expect_no_call "--env-file .env.previous"
  expect_output "v0.2.0 did not become ready after its migrations were applied"
  expect_head "$COMMIT_V020"
  release_env_text 0.2.0
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  release_text 0.1.0
  expect_file "$COMPOSE_FOLDER/.env.previous" "$TEXT"
  passed
}

scenario_degraded() {
  new_world "a Degraded API passes with a warning"
  state docker/0.2.0/ready Degraded
  run_script deploy.sh v0.2.0
  expect_status 0
  expect_output "[warn] the API answers Degraded"
  expect_output "deployed v0.2.0 (commit 202020202020): the API answers Degraded"
  expect_head "$COMMIT_V020"
  passed
}

scenario_database_ahead() {
  new_world "a database of a newer release stops a deployment and a rollback"
  state docker/0.2.0/status-exit 4
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "the migrator of v0.2.0 finds migrations it does not know applied (exit 4)"
  expect_no_call "run --rm -T migrator migrate"
  expect_no_call " up --detach "
  expect_calls "git checkout --quiet --detach $COMMIT_V020" "git checkout --quiet --detach $COMMIT_V010"
  expect_unchanged
  state docker/0.0.9/status-exit 4
  run_script rollback.sh
  expect_status 1
  expect_calls "$COMPOSE --env-file .env.previous run --rm -T migrator status # 0.0.9"
  expect_output "the migrator of v0.0.9 finds migrations it does not know applied (exit 4)"
  expect_no_call "git checkout "
  expect_no_call " up --detach "
  expect_unchanged
  passed
}

scenario_rerun_keeps_previous() {
  new_world "deploying the release .env names already keeps .env.previous"
  move_head_to "$COMMIT_V020"
  release_env_text 0.2.0
  write_text "$COMPOSE_FOLDER/.env"
  release_text 0.1.0
  write_text "$COMPOSE_FOLDER/.env.previous"
  run_script deploy.sh v0.2.0
  expect_status 0
  expect_no_call "git checkout "
  expect_output ".env names v0.2.0 already, so .env.previous keeps the release it names"
  release_env_text 0.2.0
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  release_text 0.1.0
  expect_file "$COMPOSE_FOLDER/.env.previous" "$TEXT"
  expect_head "$COMMIT_V020"
  passed
}

scenario_failed_rerun_stays() {
  new_world "a failed rollout of the release .env names already stays on it and changes no file"
  move_head_to "$COMMIT_V020"
  release_env_text 0.2.0
  write_text "$COMPOSE_FOLDER/.env"
  release_text 0.1.0
  write_text "$COMPOSE_FOLDER/.env.previous"
  state docker/0.2.0/up-exit 1
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "v0.2.0 did not become ready, and .env named it before this run, so the server stays on v0.2.0"
  expect_call_count 1 " up --detach "
  expect_no_call "--env-file .env.previous"
  expect_no_call "git checkout "
  release_env_text 0.2.0
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  release_text 0.1.0
  expect_file "$COMPOSE_FOLDER/.env.previous" "$TEXT"
  expect_head "$COMMIT_V020"
  passed
}

scenario_dry_run() {
  new_world "a dry run checks and pulls, and changes neither .env nor the checkout"
  run_script deploy.sh --dry-run v0.2.0
  expect_status 0
  expect_calls "git diff --name-only $COMMIT_V010 $COMMIT_V020 -- ${HOST_PATHS[*]}" \
    "$COMPOSE --env-file candidate.env pull api web proxy # 0.2.0"
  expect_no_call "git checkout "
  expect_no_call "target deploy.sh"
  expect_no_call " run --rm "
  expect_no_call " up --detach "
  expect_output "dry run: v0.2.0 (commit 202020202020) is verified and its images are pulled"
  expect_unchanged
  passed
}

scenario_rollback() {
  new_world "a rollback asks the replaced release's migrator, swaps .env and .env.previous and rolls back"
  run_script rollback.sh --dry-run
  expect_status 0
  expect_calls "$COMPOSE --env-file .env.previous run --rm -T migrator status # 0.0.9"
  expect_no_call "git checkout "
  expect_no_call " up --detach "
  expect_output "dry run: the server can go back to v0.0.9 (commit 090909090909)"
  expect_unchanged
  run_script rollback.sh
  expect_status 0
  expect_calls \
    "id -un" "flock --nonblock 9" \
    "git rev-parse --verify --quiet refs/tags/v0.0.9^{commit}" \
    "git merge-base --is-ancestor $COMMIT_V009 refs/remotes/origin/main" \
    "git status --porcelain --untracked-files=no" \
    "docker image inspect --format {{.Id}} $REGISTRY/api@${DIGESTS[api 0.0.9]}" \
    "docker image inspect --format {{.Id}} $REGISTRY/web@${DIGESTS[web 0.0.9]}" \
    "docker image inspect --format {{.Id}} $REGISTRY/proxy@${DIGESTS[proxy 0.0.9]}" \
    "$COMPOSE --env-file .env.previous run --rm -T migrator status # 0.0.9" \
    "git checkout --quiet --detach $COMMIT_V009" \
    "$COMPOSE up --detach --wait --wait-timeout 300 --no-deps --no-build --pull never api web proxy # 0.0.9" \
    "$COMPOSE exec -T proxy wget -qO- -T 5 http://api:8080/healthz/ready # 0.0.9"
  expect_output "rolled back to v0.0.9 (commit 090909090909): the API answers Healthy"
  expect_head "$COMMIT_V009"
  release_env_text 0.0.9
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  release_text 0.1.0
  expect_file "$COMPOSE_FOLDER/.env.previous" "$TEXT"
  passed
}

scenario_rollback_without_images() {
  new_world "a rollback whose images are gone changes nothing"
  printf '%s\n' "${DIGESTS[web 0.1.0]}" > "$STATE/docker/images/web"
  run_script rollback.sh
  expect_status 1
  expect_output "the web image(s) of v0.0.9 are no longer on this server"
  expect_no_call " run --rm "
  expect_unchanged
  passed
}

scenario_rollback_refused_by_migrator() {
  new_world "a rollback goes back only when the replaced release's migrator finds every migration it knows applied and no other"
  local code
  for code in 3 2 1; do
    state docker/0.0.9/status-exit "$code"
    run_script rollback.sh
    expect_status 1
    expect_calls "$COMPOSE --env-file .env.previous run --rm -T migrator status # 0.0.9"
    case "$code" in
      3) expect_output "the migrator of v0.0.9 finds migrations of its own still to apply (exit 3), which a rollback never applies; deploy v0.0.9 with deploy.sh instead" ;;
      2) expect_output "the migrator of v0.0.9 could not load its configuration (exit 2) (see its output above); nothing was changed" ;;
      1) expect_output "the migrator of v0.0.9 could not report on the database (exit 1) (see its output above); nothing was changed" ;;
    esac
    expect_no_call "git checkout "
    expect_no_call " up --detach "
    expect_unchanged
  done
  passed
}

scenario_first_deployment() {
  new_world "the first deployment adds the digest lines .env lacks, keeps no .env.previous and leaves a failed rollout for diagnosis"
  printf -v TEXT '# Stand-in production values\nERP_SITE_ADDRESS=192.0.2.10\nIMAGE_TAG=local\nAPPCONFIG_ENDPOINT=https://appcs-stand-in.azconfig.io'
  write_text "$COMPOSE_FOLDER/.env"
  rm -f -- "$COMPOSE_FOLDER/.env.previous"
  run_script deploy.sh v0.2.0
  expect_status 0
  expect_missing "$COMPOSE_FOLDER/.env.previous"
  printf -v TEXT '# Stand-in production values\nERP_SITE_ADDRESS=192.0.2.10\nIMAGE_TAG=0.2.0\nAPPCONFIG_ENDPOINT=https://appcs-stand-in.azconfig.io\nAPI_IMAGE_DIGEST=%s\nWEB_IMAGE_DIGEST=%s\nPROXY_IMAGE_DIGEST=%s' \
    "${DIGESTS[api 0.2.0]}" "${DIGESTS[web 0.2.0]}" "${DIGESTS[proxy 0.2.0]}"
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  expect_call_count 9 "docker image rm "

  env_text local '' '' ''
  write_text "$COMPOSE_FOLDER/.env"
  state docker/0.2.0/up-exit 1
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "v0.2.0 did not become ready, and no other release ran here to go back to"
  expect_call_count 1 " up --detach "
  expect_missing "$COMPOSE_FOLDER/.env.previous"
  release_env_text 0.2.0
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  expect_head "$COMMIT_V020"

  env_text local '' '' ''
  write_text "$COMPOSE_FOLDER/.env"
  release_text 0.1.0
  write_text "$COMPOSE_FOLDER/.env.previous"
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_output "names the release v0.1.0, but .env names none"
  expect_no_call "git fetch "
  passed
}

scenario_failed_rollout_after_earlier_migrations() {
  new_world "a failed rollout stays when the replaced release's migrator finds the database ahead of it"
  state docker/0.2.0/up-exit 1
  state docker/0.1.0/status-exit 4
  run_script deploy.sh v0.2.0
  expect_status 1
  expect_calls "$COMPOSE --env-file .env.previous run --rm -T migrator status # 0.1.0"
  expect_call_count 1 " up --detach "
  expect_output "the migrator of v0.1.0 finds migrations it does not know applied (exit 4): the database belongs to a newer release, so the server stays on v0.2.0"
  expect_head "$COMMIT_V020"
  release_env_text 0.2.0
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  release_text 0.1.0
  expect_file "$COMPOSE_FOLDER/.env.previous" "$TEXT"
  passed
}

scenario_verify_release_image() {
  new_world "verify_release_image, which the release workflow calls, refuses malformed input before gh"
  local bundle="$STATE/release/v0.2.0/api.provenance.sigstore.json"
  local digest="${DIGESTS[api 0.2.0]}" provenance=https://slsa.dev/provenance/v1
  local -a cases=(
    "db $digest $bundle $provenance v0.2.0 $COMMIT_V020"
    "api sha256:A020 $bundle $provenance v0.2.0 $COMMIT_V020"
    "api $digest $bundle https://example.com/predicate v0.2.0 $COMMIT_V020"
    "api $digest $bundle $provenance 0.2.0 $COMMIT_V020"
    "api $digest $bundle $provenance v0.2.0 2020"
    "api $digest $STATE/missing.json $provenance v0.2.0 $COMMIT_V020"
  )
  local arguments
  local -a words=()
  for arguments in "${cases[@]}"; do
    read -r -a words <<< "$arguments"
    run_function verify_release_image "${words[@]}"
    expect_status 1
    expect_only_calls
  done
  run_function verify_release_image api "$digest" "$bundle" "$provenance" v0.2.0 "$COMMIT_V020"
  expect_status 0
  verify_call api provenance 0.2.0 "$COMMIT_V020"
  expect_only_calls "gh --version" "$TEXT"
  passed
}

run_function() {
  : > "$STATE/calls"
  STATUS=0
  OUTPUT="$(cd -- "$WORK" && env PATH="$BIN:$PATH" bash -c 'source "$1"; shift; "$@"' release "$CHECKOUT/scripts/deploy/lib/release.sh" "$@" 2>&1)" \
    || STATUS=$?
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  require_command bash cp mkdir mktemp find grep
  prepare_work
  scenario_deployment_order
  scenario_target_check_refuses
  scenario_malformed_tags
  scenario_malformed_release_files
  scenario_refused_bundle
  scenario_other_repository
  scenario_commit_off_main
  scenario_old_gh
  scenario_local_changes
  scenario_lock_held
  scenario_lock_released_while_waiting
  scenario_other_account
  scenario_other_folder
  scenario_restart_window
  scenario_failed_migration
  scenario_record_failure_after_migrations
  scenario_failed_rollout_goes_back
  scenario_failed_rollout_after_migrations
  scenario_degraded
  scenario_database_ahead
  scenario_rerun_keeps_previous
  scenario_failed_rerun_stays
  scenario_dry_run
  scenario_rollback
  scenario_rollback_without_images
  scenario_rollback_refused_by_migrator
  scenario_first_deployment
  scenario_failed_rollout_after_earlier_migrations
  scenario_verify_release_image
  printf 'release decisions ok: %s scenarios\n' "$PASSED"
}

main "$@"
