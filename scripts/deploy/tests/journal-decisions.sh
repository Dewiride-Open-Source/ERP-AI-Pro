#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/tests/journal-decisions.sh

Runs journal.sh in a temporary folder that stands for the server'"'"'s root (ERP_DEPLOY_TEST_ROOT names it to it), with
tests/stand-in.sh on PATH as docker, flock, id, date and journalctl, recording every call, and the real OpenSSL, gzip and
coreutils. The stand-in journalctl serves a file of JSON entries between the bounds it is given and takes exactly the
arguments journal.sh passes, --all among them, and the stand-in docker plays the Azure CLI of compose.backup.yaml on a
folder that stands for the container host-journal, noting whether the deployment lock is held for each call. Its .env names
no BACKUP_CONTAINER, which journal.sh does not read. It checks each decision the script makes:
  - a nightly run copies each India day of the last 30 up to yesterday of which the container holds no copy and the journal
    holds entries, the oldest first, asking journalctl for the day from 18:30:00 UTC the day before to 18:29:59.999999 UTC,
    so an entry of a day'"'"'s first or last microsecond lands in that day alone; it seals each day to a throwaway RSA 4096
    certificate with AES-256-GCM and RSA-OAEP-SHA256, uploads it as <yyyy>/<mm>/<yyyymmdd>.json.gz.p7m without writing over
    a blob and reads it back, skips a day already copied and one the journal holds no entry of, catches up missed nights
    across a month and a year within the 30 days and none before, and deletes, naming each, only the copies the storage
    service created more than 366 days ago, never taking the deployment lock nor calling the Azure CLI while it is held; each
    copy opens with the throwaway key into exactly the entries of its day;
  - a copy of a day that the storage service created more than an hour before the day ended is reported as one that cannot
    hold it once the other days are copied, and the run fails;
  - a run without the group systemd-journal, a failed listing, a listing that names a copy twice or gives a creation that
    is not a time, a failed journalctl, a failed upload, a copy that reads back otherwise, a refused delete, a missing
    certificate, another account and an argument stop it, the failure naming its step, the days it uploaded and the copies
    it deleted;
  - --fetch, for one day or with --through for a period, downloads each day'"'"'s copy, checks it against the container'"'"'s
    record, opens it with the key, writes exactly the day'"'"'s entries to its own 0600 file in a new 0700 folder and shreds
    the key once, after the last day; it refuses, keeping the key and writing nothing, a period that reaches a day not over,
    one of more than 31 days, a date that is no day, a --through before the first day, a folder that exists, a missing copy,
    a copy created before its day ended, a download that differs from the container'"'"'s record and a key that does not
    open the copy (naming the certificate it needs); a copy whose entries lie outside its day stops a period midway, the
    days before it written and named and the key kept;
  - --list shows the copies by their creation, the newest first, marks a copy created before its day ended and one whose
    name is no day, and leaves other blobs out.
It needs bash with the usual command-line tools (coreutils, grep, sed, awk, gzip) and OpenSSL 3; check.sh runs it.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=../lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/../lib/common.sh"

readonly STAND_IN_COMMANDS=(docker flock id date journalctl)
readonly TREE_FILES=(scripts/deploy/journal.sh scripts/deploy/lib/common.sh scripts/deploy/lib/release.sh scripts/deploy/lib/backup.sh
  infra/compose/compose.backup.yaml)
readonly NOW='2026-10-30T21:00:00Z'
readonly CLIENT_ID='22222222-2222-2222-2222-222222222222'
readonly TENANT_ID='11111111-1111-1111-1111-111111111111'
readonly CONTAINER='host-journal'
readonly COMPOSE_CALL='docker compose --progress quiet -f compose.backup.yaml'
readonly RUN="$COMPOSE_CALL run --rm -T --quiet-pull"
readonly AS_BACKUP="-e BACKUP_CLIENT_ID=$CLIENT_ID -e BACKUP_TENANT_ID=$TENANT_ID"
readonly STORAGE="--auth-mode login --blob-endpoint https://sterpaiprobackup.blob.core.windows.net --container-name $CONTAINER"
readonly PROPERTIES='--query [properties.contentLength, properties.contentSettings.contentMd5] --output tsv'
readonly FETCH_PROPERTIES='--query [properties.contentLength, properties.contentSettings.contentMd5, properties.creationTime] --output tsv'
readonly LISTING="$RUN $AS_BACKUP azure-cli storage blob list $STORAGE --num-results * --query [].[name, properties.creationTime] --output tsv"
readonly EXPORT='journalctl --output=json --all --no-pager --quiet'

WORK=''
ROOT=''
CHECKOUT=''
COMPOSE_FOLDER=''
HOME_FOLDER=''
BIN=''
STATE=''
KEYS=''
RUNTIME=''
SHM=''
KEY_FILE=''
OPENED=''
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

make_certificate() {
  local name="$1" bits="$2"
  local -a serial=()
  if [[ -n "${3:-}" ]]; then
    serial=(-set_serial "$3")
  fi
  MSYS2_ARG_CONV_EXCL='/CN=' openssl req -x509 -newkey "rsa:$bits" -noenc -days 3650 -subj "/CN=erp-journal-decisions-$name" \
    "${serial[@]}" -keyout "$KEYS/$name-key.pem" -out "$KEYS/$name.crt" 2> /dev/null || die "cannot make the throwaway certificate $name"
  cat -- "$KEYS/$name-key.pem" "$KEYS/$name.crt" > "$KEYS/$name-bundle.pem"
}

prepare_work() {
  WORK="$(cd -- "$(mktemp -d)" && pwd -P)"
  trap remove_work EXIT
  ROOT="$WORK/root"
  CHECKOUT="$ROOT$DEPLOY_CHECKOUT"
  COMPOSE_FOLDER="$CHECKOUT/infra/compose"
  HOME_FOLDER="$ROOT$DEPLOY_HOME"
  BIN="$WORK/bin"
  STATE="$WORK/state"
  KEYS="$WORK/keys"
  RUNTIME="$WORK/run/erp-ai-pro-journal"
  SHM="$ROOT/dev/shm"
  KEY_FILE="$HOME_FOLDER/erp-backup-recipient-key.pem"
  mkdir -p -- "$BIN" "$KEYS"
  local command file
  for command in "${STAND_IN_COMMANDS[@]}"; do
    cp -- "$SCRIPT_DIR/tests/stand-in.sh" "$BIN/$command"
    chmod +x -- "$BIN/$command"
  done
  for file in "${TREE_FILES[@]}"; do
    mkdir -p -- "$CHECKOUT/${file%/*}"
    cp -- "$REPO_ROOT/$file" "$CHECKOUT/$file"
  done
  make_certificate recipient 4096 0x6E3C1E1C7D635B1FAF5CAB7B9E9C8C4D
  make_certificate other 2048
}

state() {
  mkdir -p -- "$(dirname -- "$STATE/$1")"
  printf '%s\n' "$2" > "$STATE/$1"
}

env_text() {
  printf '# Stand-in production values\nERP_SITE_ADDRESS=192.0.2.10\nIMAGE_TAG=0.3.0\nAZURE_TENANT_ID=%s\nBACKUP_BLOB_ENDPOINT=https://sterpaiprobackup.blob.core.windows.net/\nBACKUP_AZURE_CLIENT_ID=%s\n' \
    "$TENANT_ID" "$CLIENT_ID"
}

new_world() {
  SCENARIO="$1"
  OUTPUT=''
  rm -rf -- "$STATE" "$WORK/run" "$WORK/tmp" "$SHM" "$KEY_FILE" "$COMPOSE_FOLDER/secrets" "$COMPOSE_FOLDER/.env" "$HOME_FOLDER/records"
  rm -rf -- "$HOME_FOLDER"/journal-*
  mkdir -p -- "$STATE/blobs/$CONTAINER" "$STATE/journal" "$RUNTIME" "$WORK/tmp" "$SHM" "$COMPOSE_FOLDER/secrets"
  chmod 700 -- "$RUNTIME"
  state epoch "$(date -u -d "$NOW" +%s)"
  env_text > "$COMPOSE_FOLDER/.env"
  printf 'stand-in client certificate\n' > "$COMPOSE_FOLDER/secrets/erp-backup-client.pem"
  cp -- "$KEYS/recipient.crt" "$COMPOSE_FOLDER/secrets/erp-backup-recipient.crt"
  : > "$STATE/journal/entries"
  : > "$STATE/calls"
}

entry() {
  local seconds
  seconds="$(date -u -d "${1%.*}" +%s)" || die "cannot read the entry time $1"
  printf '{"__CURSOR":"s=0c0ffee;i=%s","__REALTIME_TIMESTAMP":"%s%s","__MONOTONIC_TIMESTAMP":"1000000","_BOOT_ID":"5f2c3a1b9d8e4f7a8b6c5d4e3f2a1b0c","_TRANSPORT":"journal","CONTAINER_TAG":"erp-ai-pro-api","SYSLOG_IDENTIFIER":"erp-ai-pro-api","MESSAGE":"%s"}\n' \
    "$seconds" "$seconds" "${1##*.}" "$2" >> "$STATE/journal/entries"
}

day_lines() {
  grep -F -- "\"MESSAGE\":\"day $1 " "$STATE/journal/entries" || true
}

copy_blob() {
  local name="$1" created="$2"
  mkdir -p -- "$(dirname -- "$STATE/blobs/$CONTAINER/$name")" "$(dirname -- "$STATE/blobs-created/$CONTAINER/$name")"
  printf 'stand-in copy %s\n' "$name" > "$STATE/blobs/$CONTAINER/$name"
  printf '%s\n' "$created" > "$STATE/blobs-created/$CONTAINER/$name"
}

forge_copy() {
  local name="$1" created="$2" date="$3" folder="$WORK/forged"
  mkdir -p -- "$folder" "$(dirname -- "$STATE/blobs/$CONTAINER/$name")" "$(dirname -- "$STATE/blobs-created/$CONTAINER/$name")"
  day_lines "$date" > "$folder/day.json"
  gzip -n -c -- "$folder/day.json" > "$folder/day.json.gz"
  openssl cms -encrypt -binary -aes-256-gcm -in "$folder/day.json.gz" -outform DER -out "$STATE/blobs/$CONTAINER/$name" \
    -recip "$KEYS/recipient.crt" -keyopt rsa_padding_mode:oaep -keyopt rsa_oaep_md:sha256 -keyopt rsa_mgf1_md:sha256 \
    || fail "cannot seal the forged copy $name"
  printf '%s\n' "$created" > "$STATE/blobs-created/$CONTAINER/$name"
  rm -rf -- "$folder"
}

run_journal() {
  : > "$STATE/calls"
  : > "$STATE/lock-states"
  STATUS=0
  OUTPUT="$(cd -- "$WORK" && env -u DRY_RUN PATH="$BIN:$PATH" ERP_DEPLOY_TEST_ROOT="$ROOT" TMPDIR="$WORK/tmp" RUNTIME_DIRECTORY="$RUNTIME" \
    bash "$CHECKOUT/scripts/deploy/journal.sh" "$@" 2>&1)" || STATUS=$?
}

run_by_hand() {
  : > "$STATE/calls"
  : > "$STATE/lock-states"
  STATUS=0
  OUTPUT="$(cd -- "$WORK" && env -u DRY_RUN -u RUNTIME_DIRECTORY PATH="$BIN:$PATH" ERP_DEPLOY_TEST_ROOT="$ROOT" TMPDIR="$WORK/tmp" \
    bash "$CHECKOUT/scripts/deploy/journal.sh" "$@" 2>&1)" || STATUS=$?
}

expect_status() {
  [[ "$STATUS" == "$1" ]] || fail "exited $STATUS, not $1"
}

expect_output() {
  [[ "$OUTPUT" == *"$1"* ]] || fail "printed nothing like: $1"
}

expect_no_output() {
  [[ "$OUTPUT" != *"$1"* ]] || fail "printed: $1"
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

expect_blobs() {
  local listed
  listed="$(cd -- "$STATE/blobs/$CONTAINER" && find . -type f | sed 's|^\./||' | sort | paste -sd ' ' -)"
  [[ "$listed" == "$*" ]] || fail "the container holds '$listed', not '$*'"
}

expect_key_kept() {
  [[ -f "$KEY_FILE" ]] || fail "the refused fetch removed the key"
  expect_output "the key stays at $KEY_FILE"
}

passed() {
  expect_no_call UNEXPECTED
  [[ -z "$(ls -A -- "$RUNTIME")" ]] || fail "left $(ls -A -- "$RUNTIME") in the runtime folder"
  [[ -z "$(ls -A -- "$SHM")" ]] || fail "left $(ls -A -- "$SHM") in /dev/shm"
  if [[ -n "$(find "$HOME_FOLDER" -path "$CHECKOUT" -prune -o -name '*.staged.*' -print)" ]]; then
    fail "left a staged file next to an output"
  fi
  PASSED=$(( PASSED + 1 ))
  log_info "ok: $SCENARIO"
}

blob_call() {
  local action="$1"
  shift
  if [[ "$action" == upload ]]; then
    printf '%s %s -v day.json.gz.p7m:/backup/archive.tar.p7m:ro azure-cli storage blob upload %s %s' "$RUN" "$AS_BACKUP" "$STORAGE" "$*"
  else
    printf '%s %s azure-cli storage blob %s %s %s' "$RUN" "$AS_BACKUP" "$action" "$STORAGE" "$*"
  fi
}

upload_call() {
  blob_call upload --name "$1" --file /backup/archive.tar.p7m --overwrite false --validate-content --no-progress --output none
}

export_call() {
  printf '%s --since %s --until %s' "$EXPORT" "$1" "$2"
}

open_copy() {
  local folder="$WORK/opened"
  rm -rf -- "$folder"
  mkdir -p -- "$folder"
  openssl cms -decrypt -binary -inform DER -in "$STATE/blobs/$CONTAINER/$1" -inkey "$KEYS/recipient-bundle.pem" -out "$folder/day.json.gz" \
    2> /dev/null || fail "the throwaway key does not open $1"
  gzip -d -c -- "$folder/day.json.gz" > "$folder/day.json" || fail "the opened copy $1 is not gzip data"
  OPENED="$folder/day.json"
}

expect_copy_of() {
  local name="$1" date="$2" sealed="$STATE/blobs/$CONTAINER/$1" structure expected
  structure="$(openssl cms -cmsout -print -inform DER -in "$sealed")" || fail "$name is not a CMS structure"
  [[ "$structure" == *'id-smime-ct-authEnvelopedData'* ]] || fail "$name is not CMS AuthEnvelopedData"
  [[ "$structure" == *'aes-256-gcm'* ]] || fail "$name is not encrypted with AES-256-GCM"
  [[ "$structure" == *'rsaesOaep'* ]] || fail "the key of $name is not wrapped with RSA-OAEP"
  [[ "$(openssl asn1parse -inform DER -in "$sealed" | grep -cE 'OBJECT +:sha256')" -ge 2 ]] \
    || fail "RSA-OAEP does not use SHA-256 for both the hash and MGF1 in $name"
  open_copy "$name"
  expected="$(day_lines "$date")"
  [[ -n "$expected" ]] || fail "the scenario holds no entry of $date"
  [[ "$(< "$OPENED")" == "$expected" ]] || fail "$name opens into '$(< "$OPENED")', not the entries of $date: $expected"
  rm -rf -- "$WORK/opened"
}

nightly_entries() {
  entry '2026-09-20 10:00:00.000000' 'day 2026-09-20 outside the 30 days'
  entry '2026-09-30 18:29:59.999999' 'day 2026-09-30 its last microsecond, outside the 30 days'
  entry '2026-09-30 18:30:00.000000' 'day 2026-10-01 its first microsecond'
  entry '2026-10-27 18:30:00.000000' 'day 2026-10-28 its first microsecond'
  entry '2026-10-28 09:15:00.250000' 'day 2026-10-28 a request'
  entry '2026-10-28 18:29:59.999999' 'day 2026-10-28 its last microsecond'
  entry '2026-10-29 12:00:00.000000' 'day 2026-10-29 a request'
  entry '2026-10-29 18:29:59.999999' 'day 2026-10-29 its last microsecond'
  entry '2026-10-29 18:30:00.000000' 'day 2026-10-30 its first microsecond'
  entry '2026-10-30 10:00:00.000000' 'day 2026-10-30 a request'
  entry '2026-10-30 18:29:59.999999' 'day 2026-10-30 its last microsecond'
  entry '2026-10-30 18:30:00.000000' 'day 2026-10-31 its first microsecond, today'
  entry '2026-10-30 20:59:00.000000' 'day 2026-10-31 a request, today'
}

scenario_nightly() {
  new_world "a nightly run copies each day of the last 30 up to yesterday that has no copy and holds entries, within its UTC bounds, reads each back and deletes only the copies created more than 366 days ago"
  nightly_entries
  copy_blob 2026/10/20261029.json.gz.p7m 2026-10-29T21:00:05+00:00
  copy_blob 2025/10/20251027.json.gz.p7m 2025-10-27T21:00:00+00:00
  copy_blob 2025/10/20251028.json.gz.p7m 2025-10-29T20:59:59+00:00
  copy_blob 2025/10/20251029.json.gz.p7m 2025-10-29T21:00:00+00:00
  copy_blob 2025/11/20251101.json.gz.p7m 2025-11-01T21:00:00+00:00
  copy_blob notes.txt 2025-01-01T00:00:00+00:00
  run_journal
  expect_status 0
  expect_calls "id -un" "date -u +%s" "id -Gn" "$COMPOSE_CALL pull --policy missing azure-cli" "$LISTING" \
    "$(export_call '2026-09-30 18:30:00 UTC' '2026-10-01 18:29:59.999999 UTC')" \
    "$(upload_call 2026/10/20261001.json.gz.p7m)" "$(blob_call show --name 2026/10/20261001.json.gz.p7m "$PROPERTIES")" \
    "$(export_call '2026-10-26 18:30:00 UTC' '2026-10-27 18:29:59.999999 UTC')" \
    "$(export_call '2026-10-27 18:30:00 UTC' '2026-10-28 18:29:59.999999 UTC')" \
    "$(upload_call 2026/10/20261028.json.gz.p7m)" "$(blob_call show --name 2026/10/20261028.json.gz.p7m "$PROPERTIES")" \
    "$(export_call '2026-10-29 18:30:00 UTC' '2026-10-30 18:29:59.999999 UTC')" \
    "$(upload_call 2026/10/20261030.json.gz.p7m)" "$(blob_call show --name 2026/10/20261030.json.gz.p7m "$PROPERTIES")" \
    "$(blob_call delete --name 2025/10/20251027.json.gz.p7m --output none)" \
    "$(blob_call delete --name 2025/10/20251028.json.gz.p7m --output none)"
  expect_no_call "--since 2026-10-28 18:30:00 UTC"
  expect_no_call "--since 2026-09-29 18:30:00 UTC"
  expect_no_call "--since 2026-10-30 18:30:00 UTC"
  expect_no_call "upload --name 2026/10/20261027.json.gz.p7m"
  expect_no_call "delete --name 2025/10/20251029.json.gz.p7m"
  expect_no_call "delete --name 2025/11/20251101.json.gz.p7m"
  expect_no_call "delete --name 2026/10/20261029.json.gz.p7m"
  expect_no_call "delete --name notes.txt"
  expect_no_call "flock"
  grep -q '^free azure-cli storage blob upload ' "$STATE/lock-states" || fail "noted no lock state for an upload"
  if grep -q '^held' "$STATE/lock-states"; then
    fail "ran a call with the deployment lock held: $(grep '^held' "$STATE/lock-states" | head -n 1)"
  fi
  [[ "$(grep -c '^journalctl ' "$STATE/calls")" == 29 ]] || fail "asked journalctl for $(grep -c '^journalctl ' "$STATE/calls") days, not the 29 without a copy"
  expect_blobs 2025/10/20251029.json.gz.p7m 2025/11/20251101.json.gz.p7m 2026/10/20261001.json.gz.p7m 2026/10/20261028.json.gz.p7m \
    2026/10/20261029.json.gz.p7m 2026/10/20261030.json.gz.p7m notes.txt
  [[ "$(< "$STATE/blobs/$CONTAINER/2026/10/20261029.json.gz.p7m")" == 'stand-in copy 2026/10/20261029.json.gz.p7m' ]] \
    || fail "the copy that was there already was written over"
  expect_copy_of 2026/10/20261001.json.gz.p7m 2026-10-01
  expect_copy_of 2026/10/20261028.json.gz.p7m 2026-10-28
  expect_copy_of 2026/10/20261030.json.gz.p7m 2026-10-30
  expect_output "journal: this run reads the whole journal with the group systemd-journal"
  expect_output "list: $CONTAINER holds 5 copy(ies) and 1 other blob(s); the days from 2026-10-01 to 2026-10-30 it lacks are copied"
  expect_output "day 2026-10-28 (2026-10-27 18:30:00 UTC to 2026-10-28 18:29:59.999999 UTC): 3 entries"
  expect_output "day 2026-10-27: the journal holds no entry of it, so there is nothing to copy"
  expect_output "check: $CONTAINER/2026/10/20261030.json.gz.p7m holds the"
  expect_output "prune: deleted 2025/10/20251027.json.gz.p7m, created 2025-10-27T21:00:00+00:00"
  expect_output "prune: deleted 2025/10/20251028.json.gz.p7m, created 2025-10-29T20:59:59+00:00"
  expect_output "prune: deleted 2 copy(ies) older than 365 days, kept 6, left 1 other blob(s) alone"
  expect_output "copied the journal of 3 day(s) to $CONTAINER (2026-10-01 2026-10-28 2026-10-30); 1 day(s) had a copy already and 26 held no entry; 2 copy(ies) older than 365 days deleted"
  passed
}

scenario_catch_up() {
  new_world "a run after nights the server was off catches up every day within the 30, across a month and a year, and none before"
  state epoch "$(date -u -d 2027-01-02T21:00:00Z +%s)"
  entry '2026-12-02 18:30:00.000000' 'day 2026-12-03 its first microsecond, outside the 30 days'
  entry '2026-12-03 18:30:00.000000' 'day 2026-12-04 its first microsecond'
  entry '2026-12-31 18:29:59.999999' 'day 2026-12-31 its last microsecond'
  entry '2026-12-31 18:30:00.000000' 'day 2027-01-01 its first microsecond'
  entry '2027-01-01 18:29:59.999999' 'day 2027-01-01 its last microsecond'
  entry '2027-01-02 06:00:00.000000' 'day 2027-01-02 a request'
  run_journal
  expect_status 0
  expect_calls "$(export_call '2026-12-03 18:30:00 UTC' '2026-12-04 18:29:59.999999 UTC')" "$(upload_call 2026/12/20261204.json.gz.p7m)" \
    "$(export_call '2026-12-30 18:30:00 UTC' '2026-12-31 18:29:59.999999 UTC')" "$(upload_call 2026/12/20261231.json.gz.p7m)" \
    "$(export_call '2026-12-31 18:30:00 UTC' '2027-01-01 18:29:59.999999 UTC')" "$(upload_call 2027/01/20270101.json.gz.p7m)" \
    "$(export_call '2027-01-01 18:30:00 UTC' '2027-01-02 18:29:59.999999 UTC')" "$(upload_call 2027/01/20270102.json.gz.p7m)"
  expect_no_call "--since 2026-12-02 18:30:00 UTC"
  expect_no_call "--since 2027-01-02 18:30:00 UTC"
  expect_blobs 2026/12/20261204.json.gz.p7m 2026/12/20261231.json.gz.p7m 2027/01/20270101.json.gz.p7m 2027/01/20270102.json.gz.p7m
  expect_copy_of 2026/12/20261204.json.gz.p7m 2026-12-04
  expect_copy_of 2026/12/20261231.json.gz.p7m 2026-12-31
  expect_copy_of 2027/01/20270101.json.gz.p7m 2027-01-01
  expect_copy_of 2027/01/20270102.json.gz.p7m 2027-01-02
  expect_output "the days from 2026-12-04 to 2027-01-02 it lacks are copied"
  expect_output "copied the journal of 4 day(s) to $CONTAINER (2026-12-04 2026-12-31 2027-01-01 2027-01-02); 0 day(s) had a copy already and 26 held no entry"
  passed
}

scenario_premade() {
  new_world "a copy the storage service created more than an hour before its day ended is reported once the other days are copied, and one within the hour counts as the day's"
  nightly_entries
  copy_blob 2026/10/20261030.json.gz.p7m 2026-10-30T17:29:59+00:00
  copy_blob 2026/10/20261028.json.gz.p7m 2026-10-28T17:30:00+00:00
  copy_blob 2025/10/20251027.json.gz.p7m 2025-10-27T21:00:00+00:00
  run_journal
  expect_status 1
  expect_output "[warn] day 2026-10-30: the storage service created 2026/10/20261030.json.gz.p7m at 2026-10-30T17:29:59+00:00, before the day ended, so it cannot hold the day"
  expect_output "the storage service created 2026/10/20261030.json.gz.p7m before their day ended, so they cannot hold it"
  expect_output "the journal copy stopped at the step 'copies': the copies of 2026-10-01 2026-10-29 are in the container, and 1 older copy(ies) were deleted before it stopped"
  expect_no_call "--since 2026-10-27 18:30:00 UTC"
  expect_no_call "--since 2026-10-29 18:30:00 UTC"
  expect_calls "$(export_call '2026-10-28 18:30:00 UTC' '2026-10-29 18:29:59.999999 UTC')" "$(upload_call 2026/10/20261029.json.gz.p7m)" \
    "$(blob_call delete --name 2025/10/20251027.json.gz.p7m --output none)"
  [[ "$(< "$STATE/blobs/$CONTAINER/2026/10/20261030.json.gz.p7m")" == 'stand-in copy 2026/10/20261030.json.gz.p7m' ]] \
    || fail "the copy made in advance was written over"
  expect_copy_of 2026/10/20261029.json.gz.p7m 2026-10-29
  expect_no_output "copied the journal of"
  passed
}

scenario_read_back() {
  new_world "a copy that reads back with another MD5, or with another length, stops the run before the next day and before any delete"
  nightly_entries
  copy_blob 2025/10/20251027.json.gz.p7m 2025-10-27T21:00:00+00:00
  copy_blob 2026/10/20261001.json.gz.p7m 2026-10-01T21:00:00+00:00
  state az/show-md5 'AAAAAAAAAAAAAAAAAAAAAA=='
  run_journal
  expect_status 1
  expect_output "$CONTAINER/2026/10/20261028.json.gz.p7m reads back as"
  expect_output "with the MD5 AAAAAAAAAAAAAAAAAAAAAA==, not the"
  expect_output "so open it with journal.sh --fetch 2026-10-28 --key <file>"
  expect_output "the journal copy stopped at the step 'check 2026-10-28': the copies of 2026-10-28 are in the container, and no older copy was deleted"
  expect_no_call "upload --name 2026/10/20261030.json.gz.p7m"
  expect_no_call "storage blob delete"
  rm -f -- "$STATE/az/show-md5" "$STATE/blobs/$CONTAINER/2026/10/20261028.json.gz.p7m" "$STATE/blobs-created/$CONTAINER/2026/10/20261028.json.gz.p7m"
  state az/show-length 12
  run_journal
  expect_status 1
  expect_output "$CONTAINER/2026/10/20261028.json.gz.p7m reads back as 12 bytes"
  expect_output "the journal copy stopped at the step 'check 2026-10-28'"
  expect_no_call "storage blob delete"
  passed
}

scenario_early_stops() {
  new_world "a run without the group systemd-journal stops before it asks anything of Docker"
  nightly_entries
  state groups 'erp-ai-pro docker'
  run_journal
  expect_status 1
  expect_output "this run lacks the group systemd-journal, so journalctl would show it its own entries alone"
  expect_output "the journal copy stopped at the step 'journal': nothing was uploaded and no copy was deleted"
  expect_only_calls "id -un" "date -u +%s" "id -Gn"
  passed

  new_world "a missing client certificate or a recipient certificate that holds a private key stops the run at its settings"
  nightly_entries
  rm -f -- "$COMPOSE_FOLDER/secrets/erp-backup-client.pem"
  run_journal
  expect_status 1
  expect_output "secrets/erp-backup-client.pem is missing"
  expect_output "the journal copy stopped at the step 'settings': nothing was uploaded and no copy was deleted"
  expect_only_calls "id -un" "date -u +%s" "id -Gn"
  printf 'stand-in client certificate\n' > "$COMPOSE_FOLDER/secrets/erp-backup-client.pem"
  cp -- "$KEYS/recipient-bundle.pem" "$COMPOSE_FOLDER/secrets/erp-backup-recipient.crt"
  run_journal
  expect_status 1
  expect_output "holds a private key, which never belongs on the server"
  expect_output "the journal copy stopped at the step 'settings'"
  expect_only_calls "id -un" "date -u +%s" "id -Gn"
  passed

  new_world "another account and an argument stop the run before it looks at anything"
  state account runner
  run_journal
  expect_status 1
  expect_output "run this as the deploy account erp-ai-pro, not as runner"
  expect_only_calls "id -un"
  run_journal --dry-run
  expect_status 1
  expect_output "unknown option '--dry-run'"
  expect_only_calls
  run_journal now
  expect_status 1
  expect_output "unexpected argument 'now'"
  expect_only_calls
  run_journal --key "$KEY_FILE"
  expect_status 1
  expect_output "--through, --key and --output belong to --fetch <first yyyy-mm-dd>"
  expect_only_calls
  passed
}

scenario_listing_stops() {
  new_world "a listing that fails stops the run at the step 'list', before journalctl"
  nightly_entries
  state az/list-exit 1
  run_journal
  expect_status 1
  expect_output "cannot list the copies of $CONTAINER"
  expect_output "the journal copy stopped at the step 'list': nothing was uploaded and no copy was deleted"
  expect_no_call "journalctl"
  passed

  new_world "a listing that names a copy twice stops the run at the step 'list'"
  nightly_entries
  copy_blob 2026/10/20261029.json.gz.p7m 2026-10-29T21:00:05+00:00
  state az/list-extra "$(printf '2026/10/20261029.json.gz.p7m\t2026-10-30T09:00:00+00:00')"
  run_journal
  expect_status 1
  expect_output "the listing of $CONTAINER names 2026/10/20261029.json.gz.p7m twice, which the storage service never does"
  expect_output "the journal copy stopped at the step 'list'"
  expect_no_call "journalctl"
  passed

  local created
  for created in '' not-a-time '2026-10-29T21:00:05+00:00 ('; do
    new_world "a copy whose creation time is '$created' stops the run at the step 'list'"
    nightly_entries
    copy_blob 2026/10/20261029.json.gz.p7m "$created"
    run_journal
    expect_status 1
    expect_output "the copy 2026/10/20261029.json.gz.p7m reports the creation time '${created//[^[:alnum:] ._:\/@=+-]/?}', which is not a time"
    expect_output "the journal copy stopped at the step 'list'"
    expect_no_call "journalctl"
  done
  passed
}

scenario_day_stops() {
  new_world "a journalctl that fails stops the run at the day it was asked for"
  nightly_entries
  state journal/exit 1
  run_journal
  expect_status 1
  expect_output "cannot export the journal of 2026-10-01 (see the error of journalctl or gzip above)"
  expect_output "the journal copy stopped at the step 'day 2026-10-01': nothing was uploaded and no copy was deleted"
  expect_no_call "storage blob upload"
  passed

  new_world "a failed upload stops the run at its day, and nothing is deleted"
  nightly_entries
  copy_blob 2025/10/20251027.json.gz.p7m 2025-10-27T21:00:00+00:00
  copy_blob 2026/10/20261001.json.gz.p7m 2026-10-01T21:00:00+00:00
  state az/upload-exit 1
  run_journal
  expect_status 1
  expect_output "cannot upload 2026/10/20261028.json.gz.p7m into $CONTAINER"
  expect_output "the journal copy stopped at the step 'upload 2026-10-28': nothing was uploaded and no copy was deleted"
  expect_no_call "storage blob show"
  expect_no_call "storage blob delete"
  passed

  new_world "a delete refused after another one stops the run, which names the days it uploaded and counts the copy it deleted"
  nightly_entries
  copy_blob 2025/10/20251026.json.gz.p7m 2025-10-26T21:00:00+00:00
  copy_blob 2025/10/20251027.json.gz.p7m 2025-10-27T21:00:00+00:00
  state az/delete-refuses 2025/10/20251027.json.gz.p7m
  run_journal
  expect_status 1
  expect_output "prune: deleted 2025/10/20251026.json.gz.p7m, created 2025-10-26T21:00:00+00:00"
  expect_output "cannot delete 2025/10/20251027.json.gz.p7m, older than 365 days"
  expect_output "the journal copy stopped at the step 'prune': the copies of 2026-10-01 2026-10-28 2026-10-29 2026-10-30 are in the container, and 1 older copy(ies) were deleted before it stopped"
  passed
}

copied_world() {
  new_world "$1"
  nightly_entries
  run_journal
  expect_status 0
  cp -- "$KEYS/recipient-bundle.pem" "$KEY_FILE"
}

expect_written() {
  local folder="$1" date listed expected=''
  shift
  [[ -d "$folder" ]] || fail "$folder was not created"
  for date in "$@"; do
    [[ -f "$folder/journal-$date.json" ]] || fail "$folder lacks journal-$date.json"
    [[ "$(< "$folder/journal-$date.json")" == "$(day_lines "$date")" ]] \
      || fail "$folder/journal-$date.json holds '$(< "$folder/journal-$date.json")', not the entries of $date"
    expected+="journal-$date.json "
  done
  listed="$(cd -- "$folder" && find . -type f | sed 's|^\./||' | sort | paste -sd ' ' -)"
  [[ "$listed " == "$expected" ]] || fail "$folder holds '$listed', not '${expected% }'"
  if [[ "$(uname -s)" == Linux ]]; then
    [[ "$(stat -c '%a' -- "$folder")" == 700 ]] || fail "$folder is not private to the deploy account"
    [[ "$(stat -c '%a' -- "$folder"/*.json | sort -u)" == 600 ]] || fail "the days in $folder are not private to the deploy account"
  fi
}

scenario_fetch() {
  local folder="$HOME_FOLDER/journal-2026-10-30-to-2026-10-30" range="$HOME_FOLDER/records/2026-10-28-to-30"
  copied_world "--fetch writes a day into a folder of its own and --through a period, one file a day, each copy checked, the key shredded once"
  run_by_hand --fetch 2026-10-30 --key "$KEY_FILE"
  expect_status 0
  expect_calls "id -un" "date -u +%s" "$COMPOSE_CALL pull --policy missing azure-cli" \
    "$(blob_call show --name 2026/10/20261030.json.gz.p7m "$FETCH_PROPERTIES")" \
    "$(blob_call download --name 2026/10/20261030.json.gz.p7m --no-progress --output none)"
  expect_no_call "storage blob upload"
  expect_no_call "storage blob delete"
  expect_no_call "storage blob list"
  expect_no_call "journalctl"
  expect_written "$folder" 2026-10-30
  [[ ! -e "$KEY_FILE" ]] || fail "the fetch kept the key"
  expect_output "day 2026-10-30 (2026-10-29 18:30:00 UTC to 2026-10-30 18:29:59.999999 UTC): 3 entries from 2026/10/20261030.json.gz.p7m"
  expect_output "created by the storage service at 2026-10-30T21:00:00+00:00, written to $folder/journal-2026-10-30.json"
  expect_output "shredded the key file $KEY_FILE"
  expect_output "wrote the journal of 1 day(s), 2026-10-30 to 2026-10-30, from $CONTAINER into $folder (0700)"
  mkdir -p -- "$HOME_FOLDER/records"
  cp -- "$KEYS/recipient-bundle.pem" "$KEY_FILE"
  run_by_hand --fetch 2026-10-28 --through 2026-10-30 --key "$KEY_FILE" --output "$range"
  expect_status 0
  expect_calls "$(blob_call show --name 2026/10/20261028.json.gz.p7m "$FETCH_PROPERTIES")" \
    "$(blob_call download --name 2026/10/20261028.json.gz.p7m --no-progress --output none)" \
    "$(blob_call show --name 2026/10/20261029.json.gz.p7m "$FETCH_PROPERTIES")" \
    "$(blob_call download --name 2026/10/20261029.json.gz.p7m --no-progress --output none)" \
    "$(blob_call show --name 2026/10/20261030.json.gz.p7m "$FETCH_PROPERTIES")" \
    "$(blob_call download --name 2026/10/20261030.json.gz.p7m --no-progress --output none)"
  expect_written "$range" 2026-10-28 2026-10-29 2026-10-30
  [[ ! -e "$KEY_FILE" ]] || fail "the fetch kept the key"
  [[ "$(grep -c 'shredded the key file' <<< "$OUTPUT")" == 1 ]] || fail "the fetch of a period did not shred the key exactly once"
  expect_output "wrote the journal of 3 day(s), 2026-10-28 to 2026-10-30, from $CONTAINER into $range (0700)"
  passed

  copied_world "--fetch refuses, keeping the key and writing nothing, a folder that exists, a period that reaches a day not over, a date that is no day, a --through before --fetch and a period of more than 31 days"
  mkdir -p -- "$folder"
  printf 'earlier\n' > "$folder/journal-2026-10-30.json"
  run_by_hand --fetch 2026-10-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "--output: $folder exists; move it away or name another folder"
  expect_output "no day was written"
  [[ "$(< "$folder/journal-2026-10-30.json")" == earlier ]] || fail "the refused fetch wrote into $folder"
  expect_no_call "docker compose"
  expect_key_kept
  rm -rf -- "$folder"
  run_by_hand --fetch 2026-10-29 --through 2026-10-31 --key "$KEY_FILE"
  expect_status 1
  expect_output "the India day 2026-10-31 lasts until 2026-10-31 18:29:59.999999 UTC, so it has no copy yet"
  expect_no_call "docker compose"
  expect_key_kept
  run_by_hand --fetch 2026-02-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "--fetch: '2026-02-30' is not a day of the calendar"
  expect_key_kept
  run_by_hand --fetch 2026-10-28 --through 2026-02-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "--through: '2026-02-30' is not a day of the calendar"
  expect_key_kept
  run_by_hand --fetch 2026-10-28 --through 2026-10-27 --key "$KEY_FILE"
  expect_status 1
  expect_output "--through: 2026-10-27 comes before 2026-10-28, the first day"
  expect_no_call "docker compose"
  expect_key_kept
  run_by_hand --fetch 2026-09-29 --through 2026-10-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "the period from 2026-09-29 to 2026-10-30 holds 32 days, more than the 31 one fetch takes; fetch it in parts"
  expect_no_call "docker compose"
  expect_key_kept
  run_by_hand --fetch 2026-09-30 --through 2026-10-30 --key "$KEY_FILE"
  expect_status 1
  expect_no_output "more than the 31 one fetch takes"
  expect_output "cannot read 2026/09/20260930.json.gz.p7m in $CONTAINER"
  expect_output "no day was written"
  [[ ! -e "$HOME_FOLDER/journal-2026-09-30-to-2026-10-30" ]] || fail "the refused fetch left its folder behind"
  expect_key_kept
  run_by_hand --fetch 2026-10-3 --key "$KEY_FILE"
  expect_status 1
  expect_output "--fetch: '2026-10-3' is not an India date such as 2026-10-30"
  expect_only_calls
  run_by_hand --fetch 2026-10-28 --through 2026-10-3 --key "$KEY_FILE"
  expect_status 1
  expect_output "--through: '2026-10-3' is not an India date such as 2026-10-30"
  expect_only_calls
  run_by_hand --through 2026-10-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "--through, --key and --output belong to --fetch <first yyyy-mm-dd>"
  expect_only_calls
  run_by_hand --fetch 2026-10-30
  expect_status 1
  expect_output "--key <file> is required: the private key that opens the copies"
  expect_only_calls
  run_by_hand --fetch 2026-10-30 --key "$KEYS/recipient.crt"
  expect_status 1
  expect_output "holds no private key that OpenSSL reads without a password"
  expect_only_calls "id -un"
  passed

  copied_world "--fetch refuses a missing copy, a copy created before its day ended and a download that differs from the container's record, keeping the key and writing nothing"
  run_by_hand --fetch 2026-10-27 --key "$KEY_FILE"
  expect_status 1
  expect_output "cannot read 2026/10/20261027.json.gz.p7m in $CONTAINER"
  expect_output "journal.sh --list names the copies it holds"
  expect_output "no day was written"
  expect_no_call "storage blob download"
  expect_key_kept
  forge_copy 2026/10/20261025.json.gz.p7m 2026-10-25T10:00:00+00:00 2026-10-30
  run_by_hand --fetch 2026-10-25 --key "$KEY_FILE"
  expect_status 1
  expect_output "the storage service created 2026/10/20261025.json.gz.p7m at 2026-10-25T10:00:00+00:00, before its day ended at 2026-10-25 18:29:59.999999 UTC, so it cannot hold that day"
  expect_no_call "storage blob download"
  expect_key_kept
  state az/show-md5 'AAAAAAAAAAAAAAAAAAAAAA=='
  run_by_hand --fetch 2026-10-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "the download of 2026/10/20261030.json.gz.p7m does not match its length"
  expect_output "and MD5 AAAAAAAAAAAAAAAAAAAAAA== in the container"
  [[ ! -e "$folder" ]] || fail "the refused fetch left its folder behind"
  expect_key_kept
  passed

  local midway="$HOME_FOLDER/journal-2026-10-28-to-2026-10-30"
  copied_world "a fetch that a copy stops midway keeps the days it wrote, names them and keeps the key, and a key that opens no copy writes nothing"
  forge_copy 2026/10/20261029.json.gz.p7m 2026-10-29T21:00:00+00:00 2026-10-30
  run_by_hand --fetch 2026-10-28 --through 2026-10-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "3 of the 3 entries of 2026/10/20261029.json.gz.p7m lie outside the India day 2026-10-29 (2026-10-28 18:30:00 UTC to 2026-10-29 18:29:59.999999 UTC), so it is no copy of that day"
  expect_output "written before the failure, into $midway: 2026-10-28; once its cause is fixed, fetch the days after them into another folder"
  expect_written "$midway" 2026-10-28
  expect_no_call "download --name 2026/10/20261030.json.gz.p7m"
  expect_no_output "shredded the key file"
  expect_key_kept
  cp -- "$KEYS/other-bundle.pem" "$KEY_FILE"
  run_by_hand --fetch 2026-10-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "the key does not open 2026/10/20261030.json.gz.p7m, or the copy was changed: the copy is sealed to the certificate with the serial number $(openssl x509 -in "$KEYS/recipient.crt" -noout -serial | sed 's/^serial=0*//')"
  expect_output "and the key file holds $(openssl x509 -in "$KEYS/other.crt" -noout -serial | sed 's/^serial=0*//')"
  expect_output "no day was written"
  [[ ! -e "$folder" ]] || fail "the refused fetch left its folder behind"
  expect_key_kept
  passed
}

scenario_list() {
  new_world "--list shows the copies by their creation, the newest first, marks a copy made before its day ended and one whose name is no day, and leaves other blobs out"
  copy_blob 2026/10/20261029.json.gz.p7m 2026-10-29T21:00:05+00:00
  copy_blob 2026/10/20261030.json.gz.p7m 2026-10-30T10:00:00+00:00
  copy_blob 2025/11/20251101.json.gz.p7m 2025-11-01T21:00:00+00:00
  copy_blob 2026/02/20260231.json.gz.p7m 2026-03-01T21:00:00+00:00
  copy_blob notes.txt 2026-10-31T00:00:00+00:00
  run_by_hand --list
  expect_status 0
  [[ "$OUTPUT" == *"2026/10/20261030.json.gz.p7m   created 2026-10-30T10:00:00+00:00   created before its day ended, so it cannot hold that day"*"2026/10/20261029.json.gz.p7m   created 2026-10-29T21:00:05+00:00"*"2026/02/20260231.json.gz.p7m   created 2026-03-01T21:00:00+00:00   its name names no day of the calendar"*"2025/11/20251101.json.gz.p7m   created 2025-11-01T21:00:00+00:00"* ]] \
    || fail "the copies are not listed by their creation, the newest first, with their marks"
  expect_no_output "2026/10/20261029.json.gz.p7m   created 2026-10-29T21:00:05+00:00   "
  expect_output "4 copy(ies) in $CONTAINER, the newest creation first; 1 other blob(s) left out"
  expect_calls "id -un" "$COMPOSE_CALL pull --policy missing azure-cli" "$LISTING"
  expect_no_call "storage blob download"
  expect_no_call "journalctl"
  run_by_hand --list --key "$KEY_FILE"
  expect_status 1
  expect_output "--list takes no other option: it lists the copies and changes nothing"
  expect_only_calls
  passed
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  require_command bash cp mkdir mktemp grep sed awk date gzip openssl base64 shred stat paste sort
  prepare_work
  scenario_nightly
  scenario_catch_up
  scenario_premade
  scenario_read_back
  scenario_early_stops
  scenario_listing_stops
  scenario_day_stops
  scenario_fetch
  scenario_list
  printf 'journal decisions ok: %s scenarios\n' "$PASSED"
}

main "$@"
