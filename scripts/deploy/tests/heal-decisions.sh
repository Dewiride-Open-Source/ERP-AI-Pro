#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/tests/heal-decisions.sh

Runs heal.sh in a temporary folder that stands for the server'"'"'s root (ERP_DEPLOY_TEST_ROOT names it to it), with
tests/stand-in.sh on PATH as docker, flock, id and date, recording every call. It checks each decision heal.sh makes: an
unhealthy container that has run 30 minutes or more is restarted and one that started later is left alone, each with a
line; a restart that fails makes the run fail after the others were handled; a held lock and a healthy stack change
nothing; another account and an argument stop it before it looks at the stack. It needs bash with the usual command-line
tools (coreutils, grep, sed) and nothing else; check.sh runs it.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=../lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/../lib/common.sh"

readonly STAND_IN_COMMANDS=(docker flock id date)
readonly TREE_FILES=(scripts/deploy/heal.sh scripts/deploy/lib/common.sh scripts/deploy/lib/release.sh)
readonly LIST_UNHEALTHY='docker ps --filter label=com.docker.compose.project=erp-ai-pro --filter health=unhealthy --format {{.ID}} {{.Label "com.docker.compose.service"}}'
readonly NOW='2026-10-30T12:00:00Z'

WORK=''
ROOT=''
CHECKOUT=''
BIN=''
STATE=''
SCENARIO=''
STATUS=0
OUTPUT=''
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

prepare_work() {
  WORK="$(cd -- "$(mktemp -d)" && pwd -P)"
  trap remove_work EXIT
  ROOT="$WORK/root"
  CHECKOUT="$ROOT$DEPLOY_CHECKOUT"
  BIN="$WORK/bin"
  STATE="$WORK/state"
  mkdir -p -- "$BIN"
  local command file
  for command in "${STAND_IN_COMMANDS[@]}"; do
    cp -- "$SCRIPT_DIR/tests/stand-in.sh" "$BIN/$command"
    chmod +x -- "$BIN/$command"
  done
  for file in "${TREE_FILES[@]}"; do
    mkdir -p -- "$CHECKOUT/${file%/*}"
    cp -- "$REPO_ROOT/$file" "$CHECKOUT/$file"
  done
}

new_world() {
  SCENARIO="$1"
  OUTPUT=''
  rm -rf -- "$STATE" "$ROOT$DEPLOY_HOME/deploy.lock"
  mkdir -p -- "$STATE/docker/started" "$ROOT$DEPLOY_HOME"
  state epoch "$(date -u -d "$NOW" +%s)"
  : > "$STATE/calls"
}

state() {
  mkdir -p -- "$(dirname -- "$STATE/$1")"
  printf '%s\n' "$2" > "$STATE/$1"
}

unhealthy() {
  local container started
  : > "$STATE/docker/unhealthy"
  while (( $# > 0 )); do
    container="$1"
    started="$2"
    shift 2
    printf '%s\n' "$container" >> "$STATE/docker/unhealthy"
    state "docker/started/${container%% *}" "$started"
  done
}

run_heal() {
  : > "$STATE/calls"
  STATUS=0
  OUTPUT="$(cd -- "$WORK" && env -u DRY_RUN PATH="$BIN:$PATH" ERP_DEPLOY_TEST_ROOT="$ROOT" \
    bash "$CHECKOUT/scripts/deploy/heal.sh" "$@" 2>&1)" || STATUS=$?
}

expect_status() {
  [[ "$STATUS" == "$1" ]] || fail "exited $STATUS, not $1"
}

expect_output() {
  [[ "$OUTPUT" == *"$1"* ]] || fail "printed nothing like: $1"
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

passed() {
  expect_no_call UNEXPECTED
  PASSED=$(( PASSED + 1 ))
  log_info "ok: $SCENARIO"
}

scenario_restart() {
  new_world "an unhealthy container that has run 30 minutes or more is restarted, one that started later is left alone"
  unhealthy 'a1 api' 2026-10-30T11:30:01.123456789Z 'b2 web' 2026-10-30T11:30:00.999999999Z
  run_heal
  expect_status 0
  expect_only_calls "id -un" "flock --nonblock 9" "$LIST_UNHEALTHY" \
    "docker inspect --format {{.State.StartedAt}} a1" "date -u -d 2026-10-30T11:30:01.123456789Z +%s" "date -u +%s" \
    "docker inspect --format {{.State.StartedAt}} b2" "date -u -d 2026-10-30T11:30:00.999999999Z +%s" "date -u +%s" \
    "docker restart b2"
  expect_output "the api container is unhealthy, but it started 29 minute(s) ago, so it is left alone until it has run 30 minutes"
  expect_output "[warn] restarted the web container, which Docker reported unhealthy after 30 minute(s) of running"
  passed
}

scenario_failed_restart() {
  new_world "a restart that fails makes the run fail, after every other container was handled"
  unhealthy 'c3 proxy' 2026-10-30T10:00:00Z 'a1 api' 2026-10-30T10:00:00Z
  state docker/restart-exit 1
  run_heal
  expect_status 1
  expect_output "cannot restart the proxy container, which Docker reports unhealthy"
  expect_output "cannot restart the api container, which Docker reports unhealthy"
  passed
}

scenario_unreadable_start() {
  new_world "a start time that is not a time leaves the container alone and fails the run"
  unhealthy 'a1 api' 'not a time'
  run_heal
  expect_status 1
  expect_output "the api container reports the start time 'not a time', which is not a time"
  expect_no_call "docker restart "
  passed
}

scenario_lock_held() {
  new_world "a held lock leaves the stack alone that minute"
  unhealthy 'a1 api' 2026-10-30T10:00:00Z
  state flock-held yes
  run_heal
  expect_status 0
  expect_output "holds $ROOT$DEPLOY_HOME/deploy.lock, so the stack is not checked this minute"
  expect_only_calls "id -un" "flock --nonblock 9"
  passed
}

scenario_healthy() {
  new_world "a healthy stack is listed and left alone, without a line"
  run_heal
  expect_status 0
  [[ -z "$OUTPUT" ]] || fail "printed: $OUTPUT"
  expect_only_calls "id -un" "flock --nonblock 9" "$LIST_UNHEALTHY"
  passed
}

scenario_refusals() {
  new_world "another account and an argument stop it before it looks at the stack"
  state account runner
  run_heal
  expect_status 1
  expect_output "run this as the deploy account erp-ai-pro, not as runner"
  expect_only_calls "id -un"
  run_heal --dry-run
  expect_status 1
  expect_output "unknown option '--dry-run'"
  expect_only_calls
  run_heal api
  expect_status 1
  expect_output "unexpected argument 'api'"
  expect_only_calls
  passed
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  require_command bash cp mkdir mktemp grep date
  prepare_work
  scenario_restart
  scenario_failed_restart
  scenario_unreadable_start
  scenario_lock_held
  scenario_healthy
  scenario_refusals
  printf 'heal decisions ok: %s scenarios\n' "$PASSED"
}

main "$@"
