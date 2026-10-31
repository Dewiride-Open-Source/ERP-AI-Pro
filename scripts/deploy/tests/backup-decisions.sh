#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/tests/backup-decisions.sh

Runs backup.sh and restore.sh in a temporary folder that stands for the server'"'"'s root (ERP_DEPLOY_TEST_ROOT names it to
them), with tests/stand-in.sh on PATH as docker, flock, id and date, recording every call, and the real OpenSSL, tar and
coreutils. The stand-in docker plays the containers of compose.backup.yaml: the readers, writers and owners of two folders
that stand for the volumes, and the Azure CLI on a folder that stands for the backup container. It checks each decision the
scripts make:
  - a backup reads both volumes, .env and .env.previous under the lock, lets it go before the Azure CLI runs, seals them
    with their manifest to a throwaway RSA 4096 certificate with AES-256-GCM and RSA-OAEP-SHA256, uploads the archive under
    its time without writing over a blob, reads it back and deletes, naming each, only the archives the storage service
    created more than 90 days and a day ago; the archive opens with the throwaway key and every file matches the manifest;
  - a held lock, a failed volume read, a failed upload, an archive that reads back otherwise, a failed listing, a refused
    delete, a creation time that is not a time, a missing certificate, a recipient certificate that holds a private key or a
    smaller key, a malformed setting, another account and an argument stop it, the failure naming its step and the archives
    it deleted, and a recipient certificate near its end only warns;
  - a dry run of a restore opens and checks the archive and changes nothing, even while the stack runs; a restore refuses
    while the stack runs, with a key that does not open the archive (naming the certificate it needs), with an archive that
    is forged or whose files differ from its manifest, with the backup of a server whose volumes were still empty, and with
    volumes that hold files unless --replace, each before it changes anything; --list, --latest, --name and --created-before
    choose by the storage service'"'"'s creation time; a restore onto fresh volumes gives each top folder to its owner
    (1654:1654 and 10001:10001, mode 0700) before it fills it, writes .env and .env.previous as the archive holds them and
    shreds the key, and one that fails part-way names what it restored and keeps the key.
It needs bash with the usual command-line tools (coreutils, grep, sed, find, tar) and OpenSSL 3; check.sh runs it.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=../lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/../lib/common.sh"

readonly STAND_IN_COMMANDS=(docker flock id date)
readonly TREE_FILES=(scripts/deploy/backup.sh scripts/deploy/restore.sh scripts/deploy/lib/common.sh scripts/deploy/lib/release.sh
  scripts/deploy/lib/backup.sh infra/compose/compose.backup.yaml)
readonly NOW='2026-10-30T12:00:00Z'
readonly ARCHIVE='2026/10/20261030T120000Z.tar.p7m'
readonly CLIENT_ID='22222222-2222-2222-2222-222222222222'
readonly TENANT_ID='11111111-1111-1111-1111-111111111111'
readonly CONTAINER='host-backups'
readonly KEY_ID='0c6e8f43-1a2b-4c3d-8e9f-0a1b2c3d4e5f'
readonly SECOND_KEY_ID='7d3a9b21-5c4e-4f60-9a8b-1c2d3e4f5a6b'
readonly KID='https://kv-erp-ai-pro-prod.vault.azure.net/keys/Erp--Platform--DataProtection--Key/0123456789abcdef0123456789abcdef'
readonly COMPOSE_CALL='docker compose --progress quiet -f compose.backup.yaml'
readonly RUN="$COMPOSE_CALL run --rm -T --quiet-pull"
readonly AS_BACKUP="-e BACKUP_CLIENT_ID=$CLIENT_ID -e BACKUP_TENANT_ID=$TENANT_ID"
readonly STORAGE="--auth-mode login --blob-endpoint https://sterpaiprobackup.blob.core.windows.net --container-name $CONTAINER"
readonly PROPERTIES='--query [properties.contentLength, properties.contentSettings.contentMd5] --output tsv'
readonly LISTING="$RUN $AS_BACKUP azure-cli storage blob list $STORAGE --num-results * --query [].[name, properties.creationTime] --output tsv"
readonly KEY_RING_VOLUME='erp-ai-pro-data-protection-keys'
readonly PROXY_VOLUME='erp-ai-pro-proxy-data'

WORK=''
ROOT=''
CHECKOUT=''
COMPOSE_FOLDER=''
BIN=''
STATE=''
KEYS=''
RUNTIME=''
SHM=''
KEY_FILE=''
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

make_certificate() {
  local name="$1" bits="$2"
  local -a serial=()
  if [[ -n "${3:-}" ]]; then
    serial=(-set_serial "$3")
  fi
  MSYS2_ARG_CONV_EXCL='/CN=' openssl req -x509 -newkey "rsa:$bits" -noenc -days 3650 -subj "/CN=erp-backup-decisions-$name" \
    "${serial[@]}" -keyout "$KEYS/$name-key.pem" -out "$KEYS/$name.crt" 2> /dev/null || die "cannot make the throwaway certificate $name"
  cat -- "$KEYS/$name-key.pem" "$KEYS/$name.crt" > "$KEYS/$name-bundle.pem"
}

prepare_work() {
  WORK="$(cd -- "$(mktemp -d)" && pwd -P)"
  trap remove_work EXIT
  ROOT="$WORK/root"
  CHECKOUT="$ROOT$DEPLOY_CHECKOUT"
  COMPOSE_FOLDER="$CHECKOUT/infra/compose"
  BIN="$WORK/bin"
  STATE="$WORK/state"
  KEYS="$WORK/keys"
  RUNTIME="$WORK/run/erp-ai-pro-backup"
  SHM="$ROOT/dev/shm"
  KEY_FILE="$ROOT$DEPLOY_HOME/erp-backup-recipient-key.pem"
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
  make_certificate recipient 4096 0x5D2B0D0B6C524A0F9E4B9A6A8D8B7B3C
  make_certificate other 2048
}

digest_of() {
  local unit="$1" text='' index
  for (( index = 0; index < 16; index++ )); do
    text+="$unit"
  done
  printf 'sha256:%s' "$text"
}

env_text() {
  printf -v TEXT '# Stand-in production values\nERP_SITE_ADDRESS=192.0.2.10\nIMAGE_TAG=0.3.0\nAPI_IMAGE_DIGEST=%s\nWEB_IMAGE_DIGEST=%s\nPROXY_IMAGE_DIGEST=%s\nAPPCONFIG_ENDPOINT=https://appcs-stand-in.azconfig.io\nAZURE_TENANT_ID=%s\nBACKUP_BLOB_ENDPOINT=https://sterpaiprobackup.blob.core.windows.net/\nBACKUP_CONTAINER=%s\nBACKUP_AZURE_CLIENT_ID=%s' \
    "$(digest_of a030)" "$(digest_of b030)" "$(digest_of c030)" "$TENANT_ID" "$CONTAINER" "$CLIENT_ID"
}

previous_text() {
  printf -v TEXT 'IMAGE_TAG=0.2.0\nAPI_IMAGE_DIGEST=%s\nWEB_IMAGE_DIGEST=%s\nPROXY_IMAGE_DIGEST=%s' \
    "$(digest_of a020)" "$(digest_of b020)" "$(digest_of c020)"
}

key_xml() {
  printf '<?xml version="1.0" encoding="utf-8"?>\n<key id="%s" version="1">\n  <creationDate>2026-10-01T00:00:00.0000000Z</creationDate>\n  <expirationDate>%s</expirationDate>\n  <descriptor>\n    <encryptedSecret decryptorType="Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlDecryptor">\n      <encryptedKey xmlns="">\n        <kid>%s</kid>\n        <key>c3RhbmQtaW4=</key>\n      </encryptedKey>\n    </encryptedSecret>\n  </descriptor>\n</key>\n' \
    "$1" "$2" "$KID"
}

seed_volumes() {
  local keys="$STATE/volumes/$KEY_RING_VOLUME" proxy="$STATE/volumes/$PROXY_VOLUME"
  mkdir -p -- "$keys" "$proxy/caddy/pki/authorities/local"
  key_xml "$KEY_ID" 2026-12-30T00:00:00.0000000Z > "$keys/key-$KEY_ID.xml"
  key_xml "$SECOND_KEY_ID" 2027-01-28T00:00:00.0000000Z > "$keys/key-$SECOND_KEY_ID.xml"
  cp -- "$KEYS/other.crt" "$proxy/caddy/pki/authorities/local/root.crt"
  printf 'stand-in root key\n' > "$proxy/caddy/pki/authorities/local/root.key"
  state "volumes/$KEY_RING_VOLUME.top" '1654:1654 700'
  state "volumes/$PROXY_VOLUME.top" '10001:10001 700'
}

blob() {
  local name="$1" created="$2" folder
  folder="$(dirname -- "$name")"
  mkdir -p -- "$STATE/blobs/$CONTAINER/$folder" "$STATE/blobs-created/$CONTAINER/$folder"
  printf 'stand-in archive %s\n' "$name" > "$STATE/blobs/$CONTAINER/$name"
  printf '%s\n' "$created" > "$STATE/blobs-created/$CONTAINER/$name"
}

copy_archive() {
  local name="$1" created="$2" folder
  folder="$(dirname -- "$name")"
  mkdir -p -- "$STATE/blobs/$CONTAINER/$folder" "$STATE/blobs-created/$CONTAINER/$folder"
  cp -- "$STATE/blobs/$CONTAINER/$ARCHIVE" "$STATE/blobs/$CONTAINER/$name"
  printf '%s\n' "$created" > "$STATE/blobs-created/$CONTAINER/$name"
}

new_world() {
  SCENARIO="$1"
  OUTPUT=''
  rm -rf -- "$STATE" "$WORK/run" "$WORK/tmp" "$SHM" "$ROOT$DEPLOY_HOME/deploy.lock" "$KEY_FILE" "$COMPOSE_FOLDER/secrets" \
    "$COMPOSE_FOLDER/.env" "$COMPOSE_FOLDER/.env.previous"
  mkdir -p -- "$STATE/blobs/$CONTAINER" "$RUNTIME" "$WORK/tmp" "$SHM" "$COMPOSE_FOLDER/secrets"
  chmod 700 -- "$RUNTIME"
  state epoch "$(date -u -d "$NOW" +%s)"
  env_text
  printf '%s\n' "$TEXT" > "$COMPOSE_FOLDER/.env"
  previous_text
  printf '%s\n' "$TEXT" > "$COMPOSE_FOLDER/.env.previous"
  printf 'stand-in client certificate\n' > "$COMPOSE_FOLDER/secrets/erp-backup-client.pem"
  cp -- "$KEYS/recipient.crt" "$COMPOSE_FOLDER/secrets/erp-backup-recipient.crt"
  seed_volumes
  : > "$STATE/calls"
}

state() {
  mkdir -p -- "$(dirname -- "$STATE/$1")"
  printf '%s\n' "$2" > "$STATE/$1"
}

run_script() {
  local script="$1"
  shift
  : > "$STATE/calls"
  : > "$STATE/lock-states"
  STATUS=0
  OUTPUT="$(cd -- "$WORK" && env -u DRY_RUN -u RUNTIME_DIRECTORY PATH="$BIN:$PATH" ERP_DEPLOY_TEST_ROOT="$ROOT" TMPDIR="$WORK/tmp" \
    bash "$CHECKOUT/scripts/deploy/$script" "$@" 2>&1)" || STATUS=$?
}

run_backup() {
  : > "$STATE/calls"
  : > "$STATE/lock-states"
  STATUS=0
  OUTPUT="$(cd -- "$WORK" && env -u DRY_RUN PATH="$BIN:$PATH" ERP_DEPLOY_TEST_ROOT="$ROOT" TMPDIR="$WORK/tmp" RUNTIME_DIRECTORY="$RUNTIME" \
    bash "$CHECKOUT/scripts/deploy/backup.sh" "$@" 2>&1)" || STATUS=$?
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

expect_file() {
  [[ -f "$1" ]] || fail "$1 is missing"
  [[ "$(< "$1")" == "$2" ]] || fail "$1 holds: $(< "$1")"
}

expect_missing() {
  [[ ! -e "$1" ]] || fail "$1 exists"
}

expect_top() {
  [[ "$(< "$STATE/volumes/$1.top")" == "$2" ]] || fail "the top folder of $1 is $(< "$STATE/volumes/$1.top"), not $2"
}

expect_blobs() {
  local listed
  listed="$(cd -- "$STATE/blobs/$CONTAINER" && find . -type f | sed 's|^\./||' | sort | paste -sd ' ' -)"
  [[ "$listed" == "$*" ]] || fail "the container holds '$listed', not '$*'"
}

expect_unchanged_files() {
  env_text
  expect_file "$COMPOSE_FOLDER/.env" "$TEXT"
  previous_text
  expect_file "$COMPOSE_FOLDER/.env.previous" "$TEXT"
}

passed() {
  expect_no_call UNEXPECTED
  [[ -z "$(ls -A -- "$RUNTIME")" ]] || fail "left $(ls -A -- "$RUNTIME") in the runtime folder"
  [[ -z "$(ls -A -- "$SHM")" ]] || fail "left $(ls -A -- "$SHM") in /dev/shm"
  if compgen -G "$COMPOSE_FOLDER/.*.staged.*" > /dev/null; then
    fail "left a staged file in $COMPOSE_FOLDER"
  fi
  PASSED=$(( PASSED + 1 ))
  log_info "ok: $SCENARIO"
}

reader_call() {
  printf '%s %s-reader %s' "$RUN" "$1" "${*:2}"
}

blob_call() {
  local action="$1"
  shift
  if [[ "$action" == upload ]]; then
    printf '%s %s -v archive.tar.p7m:/backup/archive.tar.p7m:ro azure-cli storage blob upload %s %s' "$RUN" "$AS_BACKUP" "$STORAGE" "$*"
  else
    printf '%s %s azure-cli storage blob %s %s %s' "$RUN" "$AS_BACKUP" "$action" "$STORAGE" "$*"
  fi
}

open_archive() {
  local sealed="$1" folder="$2"
  mkdir -p -- "$folder"
  openssl cms -decrypt -binary -inform DER -in "$sealed" -inkey "$KEYS/recipient-bundle.pem" -out "$folder.tar" 2> /dev/null \
    || fail "the throwaway key does not open $sealed"
  tar -xf "$folder.tar" -C "$folder" || fail "the opened archive is not a tar archive"
}

expect_sealed_archive() {
  local sealed="$STATE/blobs/$CONTAINER/$ARCHIVE" folder="$WORK/opened" structure root line name hash size members
  structure="$(openssl cms -cmsout -print -inform DER -in "$sealed")" || fail "the archive is not a CMS structure"
  [[ "$structure" == *'id-smime-ct-authEnvelopedData'* ]] || fail "the archive is not CMS AuthEnvelopedData"
  [[ "$structure" == *'aes-256-gcm'* ]] || fail "the archive is not encrypted with AES-256-GCM"
  [[ "$structure" == *'rsaesOaep'* ]] || fail "the archive's key is not wrapped with RSA-OAEP"
  [[ "$(openssl asn1parse -inform DER -in "$sealed" | grep -cE 'OBJECT +:sha256')" -ge 2 ]] \
    || fail "RSA-OAEP does not use SHA-256 for both the hash and MGF1"
  open_archive "$sealed" "$folder"
  root="$(openssl x509 -in "$KEYS/other.crt" -noout -fingerprint -sha256)"
  env_text
  [[ "$(< "$folder/.env")" == "$TEXT" ]] || fail "the archive's .env differs from the server's"
  previous_text
  [[ "$(< "$folder/.env.previous")" == "$TEXT" ]] || fail "the archive's .env.previous differs from the server's"
  members="$(tar -tf "$folder/data-protection-keys.tar")"
  grep -qxF -- "./key-$KEY_ID.xml" <<< "$members" || fail "the key ring archive lacks key-$KEY_ID.xml"
  members="$(tar -tf "$folder/proxy-data.tar")"
  grep -qxF -- './caddy/pki/authorities/local/root.key' <<< "$members" || fail "the proxy data archive lacks the root key"
  local -a expected=(
    'ERP-AI-Pro server backup 1'
    "created $NOW"
    "release 0.3.0 $(digest_of a030) $(digest_of b030) $(digest_of c030)"
    "previous-release 0.2.0 $(digest_of a020) $(digest_of b020) $(digest_of c020)"
    "proxy-root-sha256 ${root#*=}"
    "data-protection-key $KEY_ID expires 2026-12-30T00:00:00.0000000Z wrapped-by $KID"
    "data-protection-key $SECOND_KEY_ID expires 2027-01-28T00:00:00.0000000Z wrapped-by $KID"
  )
  for line in "${expected[@]}"; do
    grep -qxF -- "$line" "$folder/manifest" || fail "the manifest lacks the line '$line': $(< "$folder/manifest")"
  done
  for name in data-protection-keys.tar proxy-data.tar .env .env.previous; do
    hash="$(sha256sum -- "$folder/$name")"
    size="$(wc -c < "$folder/$name")"
    grep -qxF -- "file ${hash%% *} ${size//[[:space:]]/} $name" "$folder/manifest" || fail "the manifest does not name $name with its hash and size"
  done
  rm -rf -- "$folder" "$folder.tar"
}

scenario_backup() {
  local action
  new_world "a backup reads and copies the files under the lock, then seals, uploads, checks and deletes only the archives the storage service created more than 90 days and a day ago, in that order"
  blob 2026/07/20260701T120000Z.tar.p7m 2026-10-29T12:00:00+00:00
  blob 2026/07/20260731T115959Z.tar.p7m 2026-07-31T11:59:59+00:00
  blob 2026/07/20260731T120000Z.tar.p7m 2026-07-31T12:00:00+00:00
  blob 2026/08/20260801T120000Z.tar.p7m 2026-08-01T12:00:00+00:00
  blob 2026/10/20261029T120000Z.tar.p7m 2026-07-22T12:00:00+00:00
  blob notes.txt 2026-01-01T00:00:00+00:00
  run_backup
  expect_status 0
  expect_calls "id -un" "date -u +%s" "$COMPOSE_CALL pull --policy missing" "flock --nonblock 9" \
    "$(reader_call key-ring tar -cf - -C /volume .)" "$(reader_call proxy-data tar -cf - -C /volume .)" \
    "$(blob_call upload --name "$ARCHIVE" --file /backup/archive.tar.p7m --overwrite false --validate-content --no-progress --output none)" \
    "$(blob_call show --name "$ARCHIVE" "$PROPERTIES")" "$LISTING" \
    "$(blob_call delete --name 2026/07/20260731T115959Z.tar.p7m --output none)" \
    "$(blob_call delete --name 2026/10/20261029T120000Z.tar.p7m --output none)"
  expect_no_call "delete --name 2026/07/20260701T120000Z.tar.p7m"
  expect_no_call "delete --name 2026/07/20260731T120000Z.tar.p7m"
  expect_no_call "delete --name notes.txt"
  expect_no_call "-writer "
  expect_no_call "-owner "
  expect_blobs 2026/07/20260701T120000Z.tar.p7m 2026/07/20260731T120000Z.tar.p7m 2026/08/20260801T120000Z.tar.p7m "$ARCHIVE" notes.txt
  grep -qxF 'held key-ring-reader tar -cf - -C /volume .' "$STATE/lock-states" || fail "the key ring was read without the lock"
  grep -qxF 'held proxy-data-reader tar -cf - -C /volume .' "$STATE/lock-states" || fail "the proxy data was read without the lock"
  if grep -q '^held azure-cli' "$STATE/lock-states"; then
    fail "the lock was still held for a call of the Azure CLI: $(grep '^held azure-cli' "$STATE/lock-states")"
  fi
  for action in upload show list delete; do
    grep -q "^free azure-cli storage blob $action " "$STATE/lock-states" || fail "no $action ran once the lock was free"
  done
  [[ "$OUTPUT" == *"release files: "*"unlock: the files are copied"*"manifest: "*"seal: "*"upload: "* ]] \
    || fail "the lock is not let go between copying the files and sealing them"
  expect_output "volume $KEY_RING_VOLUME: 2 entries"
  expect_output "volume $PROXY_VOLUME: 6 entries"
  expect_output "unlock: the files are copied, so $ROOT$DEPLOY_HOME/deploy.lock is free again"
  expect_output "manifest: release 0.3.0, 2 data protection key(s)"
  expect_output "check: $CONTAINER/$ARCHIVE holds the"
  expect_output "prune: deleted 2026/07/20260731T115959Z.tar.p7m, created 2026-07-31T11:59:59+00:00"
  expect_output "prune: deleted 2026/10/20261029T120000Z.tar.p7m, created 2026-07-22T12:00:00+00:00"
  expect_output "prune: deleted 2 archive(s) older than 90 days, kept 4, left 1 other blob(s) alone"
  expect_output "backed up the server's files to $CONTAINER/$ARCHIVE; 2 archive(s) older than 90 days deleted"
  expect_no_output "expires within 30 days"
  expect_sealed_archive
  expect_unchanged_files
  passed
}

scenario_first_release() {
  new_world "before a second deployment there is no .env.previous, and the manifest says so"
  rm -f -- "$COMPOSE_FOLDER/.env.previous"
  run_backup
  expect_status 0
  expect_output "release files: .env, and no .env.previous"
  open_archive "$STATE/blobs/$CONTAINER/$ARCHIVE" "$WORK/opened"
  expect_missing "$WORK/opened/.env.previous"
  grep -qxF 'previous-release none' "$WORK/opened/manifest" || fail "the manifest does not say previous-release none"
  rm -rf -- "$WORK/opened" "$WORK/opened.tar"
  passed
}

scenario_lock_held() {
  new_world "a lock held past the wait stops the backup before it reads anything"
  state flock-held yes
  run_backup
  expect_status 1
  expect_output "waiting up to 14 minutes"
  expect_output "the backup stopped at the step 'lock': nothing was uploaded and no archive was deleted"
  expect_calls "flock --nonblock 9" "flock --wait 840 9"
  expect_no_call "-reader "
  expect_no_call "azure-cli"
  expect_blobs
  state flock-released yes
  run_backup
  expect_status 0
  expect_calls "flock --nonblock 9" "flock --wait 840 9" "$(reader_call key-ring tar -cf - -C /volume .)"
  passed
}

scenario_failed_volume_read() {
  new_world "a volume that cannot be read stops the backup at its step"
  state docker/backup/proxy-data-reader-exit 1
  run_backup
  expect_status 1
  expect_output "cannot read the volume $PROXY_VOLUME"
  expect_output "the backup stopped at the step 'volume $PROXY_VOLUME': nothing was uploaded and no archive was deleted"
  expect_no_call "azure-cli"
  passed
}

scenario_failed_upload() {
  new_world "a failed upload stops the backup, and no archive is deleted"
  blob 2026/07/20260701T120000Z.tar.p7m 2026-07-01T12:00:00+00:00
  state az/upload-exit 1
  run_backup
  expect_status 1
  expect_output "cannot upload $ARCHIVE into $CONTAINER"
  expect_output "the backup stopped at the step 'upload': nothing was uploaded and no archive was deleted"
  expect_no_call "storage blob show"
  expect_no_call "storage blob list"
  expect_no_call "storage blob delete"
  expect_blobs 2026/07/20260701T120000Z.tar.p7m
  passed
}

scenario_existing_blob() {
  new_world "an archive of the same second is never written over"
  blob "$ARCHIVE" 2026-10-30T12:00:00+00:00
  run_backup
  expect_status 1
  expect_output "BlobAlreadyExists"
  expect_output "the backup stopped at the step 'upload'"
  [[ "$(< "$STATE/blobs/$CONTAINER/$ARCHIVE")" == "stand-in archive $ARCHIVE" ]] || fail "the blob was written over"
  passed
}

scenario_check_mismatch() {
  new_world "an archive that reads back with another MD5, or with another length, stops the backup before anything is deleted"
  blob 2026/07/20260701T120000Z.tar.p7m 2026-07-01T12:00:00+00:00
  state az/show-md5 'AAAAAAAAAAAAAAAAAAAAAA=='
  run_backup
  expect_status 1
  expect_output "with the MD5 AAAAAAAAAAAAAAAAAAAAAA==, not the"
  expect_output "the backup stopped at the step 'check': $CONTAINER/$ARCHIVE is in the container, and no older archive was deleted"
  expect_no_call "storage blob delete"
  expect_blobs 2026/07/20260701T120000Z.tar.p7m "$ARCHIVE"
  rm -f -- "$STATE/az/show-md5" "$STATE/blobs/$CONTAINER/$ARCHIVE" "$STATE/blobs-created/$CONTAINER/$ARCHIVE"
  state az/show-length 12
  run_backup
  expect_status 1
  expect_output "$CONTAINER/$ARCHIVE reads back as 12 bytes"
  expect_output "the backup stopped at the step 'check'"
  expect_no_call "storage blob delete"
  expect_blobs 2026/07/20260701T120000Z.tar.p7m "$ARCHIVE"
  passed
}

scenario_prune_listing_fails() {
  new_world "a listing that fails stops the backup at the step 'prune', its archive uploaded and nothing deleted"
  blob 2026/07/20260701T120000Z.tar.p7m 2026-07-01T12:00:00+00:00
  state az/list-exit 1
  run_backup
  expect_status 1
  expect_output "cannot list the archives of $CONTAINER"
  expect_output "the backup stopped at the step 'prune': $CONTAINER/$ARCHIVE is in the container, and no older archive was deleted"
  expect_no_call "storage blob delete"
  expect_blobs 2026/07/20260701T120000Z.tar.p7m "$ARCHIVE"
  passed
}

scenario_prune_delete_refused() {
  new_world "a delete refused after another one stops the backup, which names the archive it deleted and counts it"
  blob 2026/07/20260720T120000Z.tar.p7m 2026-07-20T12:00:00+00:00
  blob 2026/07/20260721T120000Z.tar.p7m 2026-07-21T12:00:00+00:00
  state az/delete-refuses 2026/07/20260721T120000Z.tar.p7m
  run_backup
  expect_status 1
  expect_output "prune: deleted 2026/07/20260720T120000Z.tar.p7m, created 2026-07-20T12:00:00+00:00"
  expect_output "cannot delete 2026/07/20260721T120000Z.tar.p7m, older than 90 days"
  expect_output "the backup stopped at the step 'prune': $CONTAINER/$ARCHIVE is in the container, and 1 older archive(s) were deleted before it stopped"
  expect_blobs 2026/07/20260721T120000Z.tar.p7m "$ARCHIVE"
  passed
}

scenario_prune_creation_unreadable() {
  local created
  for created in '' not-a-time; do
    new_world "an archive whose creation time is '$created' stops the backup at the step 'prune' rather than being kept"
    blob 2026/07/20260701T120000Z.tar.p7m "$created"
    run_backup
    expect_status 1
    expect_output "the archive 2026/07/20260701T120000Z.tar.p7m reports the creation time '$created', which is not a time"
    expect_output "the backup stopped at the step 'prune': $CONTAINER/$ARCHIVE is in the container, and no older archive was deleted"
    expect_blobs 2026/07/20260701T120000Z.tar.p7m "$ARCHIVE"
  done
  passed
}

scenario_expiring_recipient() {
  new_world "a recipient certificate within 30 days of its end warns, and the backup still seals to it and uploads"
  MSYS2_ARG_CONV_EXCL='/CN=' openssl req -x509 -new -key "$KEYS/recipient-key.pem" -days 1 -subj '/CN=erp-backup-decisions-expiring' \
    -out "$COMPOSE_FOLDER/secrets/erp-backup-recipient.crt" 2> /dev/null || fail "cannot make the expiring certificate"
  run_backup
  expect_status 0
  expect_output "expires within 30 days or has expired"
  expect_output "backed up the server's files to $CONTAINER/$ARCHIVE"
  open_archive "$STATE/blobs/$CONTAINER/$ARCHIVE" "$WORK/opened"
  rm -rf -- "$WORK/opened" "$WORK/opened.tar"
  passed
}

scenario_certificates() {
  new_world "a missing client certificate stops the backup at its settings, before Docker"
  rm -f -- "$COMPOSE_FOLDER/secrets/erp-backup-client.pem"
  run_backup
  expect_status 1
  expect_output "secrets/erp-backup-client.pem is missing"
  expect_output "the backup stopped at the step 'settings'"
  expect_only_calls "id -un" "date -u +%s"
  cp -- "$KEYS/recipient.crt" "$COMPOSE_FOLDER/secrets/erp-backup-client.pem"
  rm -f -- "$COMPOSE_FOLDER/secrets/erp-backup-recipient.crt"
  run_backup
  expect_status 1
  expect_output "cannot read $COMPOSE_FOLDER/secrets/erp-backup-recipient.crt"
  expect_only_calls "id -un" "date -u +%s"
  cp -- "$KEYS/recipient-bundle.pem" "$COMPOSE_FOLDER/secrets/erp-backup-recipient.crt"
  run_backup
  expect_status 1
  expect_output "holds a private key, which never belongs on the server"
  expect_only_calls "id -un" "date -u +%s"
  cp -- "$KEYS/other.crt" "$COMPOSE_FOLDER/secrets/erp-backup-recipient.crt"
  run_backup
  expect_status 1
  expect_output "does not hold an RSA key of 4096 bits or more"
  expect_only_calls "id -un" "date -u +%s"
  passed
}

scenario_settings() {
  new_world "a malformed or missing setting stops the backup at its settings"
  sed -i 's|^BACKUP_BLOB_ENDPOINT=.*|BACKUP_BLOB_ENDPOINT=http://sterpaiprobackup.blob.core.windows.net|' "$COMPOSE_FOLDER/.env"
  run_backup
  expect_status 1
  expect_output "BACKUP_BLOB_ENDPOINT in $COMPOSE_FOLDER/.env is 'http://sterpaiprobackup.blob.core.windows.net'"
  expect_only_calls "id -un" "date -u +%s"
  env_text
  printf '%s\nexport BACKUP_CONTAINER=other\n' "$TEXT" > "$COMPOSE_FOLDER/.env"
  run_backup
  expect_status 1
  expect_output "sets BACKUP_CONTAINER in another form than BACKUP_CONTAINER=<value>"
  sed -i '/^BACKUP_AZURE_CLIENT_ID=/d; /^export BACKUP_CONTAINER=/d' "$COMPOSE_FOLDER/.env"
  run_backup
  expect_status 1
  expect_output "BACKUP_AZURE_CLIENT_ID in $COMPOSE_FOLDER/.env is '', not the backup service principal's application id"
  expect_output "the backup stopped at the step 'settings'"
  passed
}

scenario_backup_refusals() {
  new_world "another account and an argument stop the backup before it looks at anything"
  state account runner
  run_backup
  expect_status 1
  expect_output "run this as the deploy account erp-ai-pro, not as runner"
  expect_only_calls "id -un"
  run_backup --dry-run
  expect_status 1
  expect_output "unknown option '--dry-run'"
  expect_only_calls
  run_backup now
  expect_status 1
  expect_output "unexpected argument 'now'"
  expect_only_calls
  passed
}

save_archive() {
  new_world "$1"
  run_backup
  expect_status 0
  rm -rf -- "$WORK/saved" "$WORK/saved-created"
  cp -R -- "$STATE/blobs" "$WORK/saved"
  cp -R -- "$STATE/blobs-created" "$WORK/saved-created"
}

restore_world() {
  new_world "$1"
  rm -rf -- "$STATE/blobs" "$STATE/blobs-created"
  cp -R -- "$WORK/saved" "$STATE/blobs"
  cp -R -- "$WORK/saved-created" "$STATE/blobs-created"
  cp -- "$KEYS/recipient-bundle.pem" "$KEY_FILE"
}

empty_volumes() {
  rm -rf -- "$STATE/volumes/$KEY_RING_VOLUME" "$STATE/volumes/$PROXY_VOLUME"
  mkdir -p -- "$STATE/volumes/$KEY_RING_VOLUME" "$STATE/volumes/$PROXY_VOLUME"
  state "volumes/$KEY_RING_VOLUME.top" '0:0 755'
  state "volumes/$PROXY_VOLUME.top" '0:0 755'
}

fresh_release_files() {
  printf 'ERP_SITE_ADDRESS=192.0.2.10\nIMAGE_TAG=local\nAZURE_TENANT_ID=%s\nBACKUP_BLOB_ENDPOINT=https://sterpaiprobackup.blob.core.windows.net/\nBACKUP_CONTAINER=%s\nBACKUP_AZURE_CLIENT_ID=%s\n' \
    "$TENANT_ID" "$CONTAINER" "$CLIENT_ID" > "$COMPOSE_FOLDER/.env"
  rm -f -- "$COMPOSE_FOLDER/.env.previous"
}

scenario_restore_fresh_server() {
  restore_world "a restore onto a rebuilt server gives each top folder to its owner, fills it and writes .env as the archive holds it"
  empty_volumes
  fresh_release_files
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 0
  expect_calls "id -un" "flock --nonblock 9" "docker ps --filter label=com.docker.compose.project=erp-ai-pro --format {{.Label \"com.docker.compose.service\"}}" \
    "$COMPOSE_CALL pull --policy missing" "$LISTING" "$(blob_call show --name "$ARCHIVE" "$PROPERTIES")" \
    "$(blob_call download --name "$ARCHIVE" --no-progress --output none)" \
    "docker volume inspect --format {{.Name}} $KEY_RING_VOLUME" "$(reader_call key-ring stat -c %u:%g %a /volume)" \
    "$RUN key-ring-owner chown 1654:1654 /volume" "$RUN key-ring-owner chmod 700 /volume" "$RUN key-ring-writer tar -xof - -C /volume" \
    "$RUN proxy-data-owner chown 10001:10001 /volume" "$RUN proxy-data-owner chmod 700 /volume" "$RUN proxy-data-writer tar -xof - -C /volume"
  expect_no_call "-writer find /volume -mindepth 1 -delete"
  expect_no_call "storage blob upload"
  expect_no_call "storage blob delete"
  expect_top "$KEY_RING_VOLUME" '1654:1654 700'
  expect_top "$PROXY_VOLUME" '10001:10001 700'
  [[ -f "$STATE/volumes/$KEY_RING_VOLUME/key-$KEY_ID.xml" ]] || fail "the key ring was not restored"
  cmp -s -- "$STATE/volumes/$PROXY_VOLUME/caddy/pki/authorities/local/root.crt" "$KEYS/other.crt" || fail "the proxy root was not restored"
  expect_unchanged_files
  if [[ "$(uname -s)" == Linux ]]; then
    [[ "$(stat -c '%a' -- "$COMPOSE_FOLDER/.env" "$COMPOSE_FOLDER/.env.previous" | sort -u)" == 600 ]] \
      || fail ".env and .env.previous are not private to the deploy account"
  fi
  expect_missing "$KEY_FILE"
  expect_output "restored the volume $KEY_RING_VOLUME: 2 entries, top folder 1654:1654 700"
  expect_output "restored the volume $PROXY_VOLUME: 6 entries, top folder 10001:10001 700"
  expect_output ".env names the release 0.3.0 of the archive; the replaced .env named none"
  expect_output "restored $ARCHIVE (made $NOW)"
  passed
}

scenario_restore_dry_run() {
  restore_world "a dry run opens and checks the archive while the stack runs and changes nothing"
  empty_volumes
  fresh_release_files
  state docker/running "$(printf 'api\nweb\nproxy')"
  run_script restore.sh --latest --key "$KEY_FILE" --dry-run
  expect_status 0
  expect_output "every file matches the manifest of $NOW"
  expect_output "the stack runs (api web proxy); a restore needs it stopped first"
  expect_output "dry run: $ARCHIVE opens with this key and matches its manifest"
  expect_output "the key stays at $KEY_FILE"
  expect_no_call "flock"
  expect_no_call "-owner "
  expect_no_call "-writer "
  expect_top "$KEY_RING_VOLUME" '0:0 755'
  [[ -z "$(ls -A -- "$STATE/volumes/$KEY_RING_VOLUME")" ]] || fail "the dry run filled the key ring"
  [[ -f "$KEY_FILE" ]] || fail "the dry run removed the key"
  [[ "$(< "$COMPOSE_FOLDER/.env")" == ERP_SITE_ADDRESS=192.0.2.10* ]] || fail "the dry run wrote .env"
  passed
}

scenario_restore_running_stack() {
  restore_world "a restore refuses while the stack runs, before it downloads anything"
  state docker/running 'api'
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "the stack runs (api); stop it first (sudo systemctl stop erp-ai-pro.service)"
  expect_no_call "azure-cli"
  expect_no_call "-owner "
  expect_unchanged_files
  [[ -f "$KEY_FILE" ]] || fail "the refused restore removed the key"
  passed
}

scenario_restore_replace() {
  restore_world "volumes that hold files are replaced only with --replace"
  printf 'newer key\n' > "$STATE/volumes/$KEY_RING_VOLUME/key-ffffffff-ffff-ffff-ffff-ffffffffffff.xml"
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "$KEY_RING_VOLUME $PROXY_VOLUME hold files, which a restore replaces only with --replace; nothing was changed"
  expect_no_call "-writer "
  [[ -f "$STATE/volumes/$KEY_RING_VOLUME/key-ffffffff-ffff-ffff-ffff-ffffffffffff.xml" ]] || fail "the refused restore changed the key ring"
  cp -- "$KEYS/recipient-bundle.pem" "$KEY_FILE"
  run_script restore.sh --latest --key "$KEY_FILE" --replace
  expect_status 0
  expect_calls "$RUN key-ring-writer find /volume -mindepth 1 -delete" "$RUN key-ring-writer tar -xof - -C /volume" \
    "$RUN proxy-data-writer find /volume -mindepth 1 -delete" "$RUN proxy-data-writer tar -xof - -C /volume"
  expect_no_call "-owner "
  expect_missing "$STATE/volumes/$KEY_RING_VOLUME/key-ffffffff-ffff-ffff-ffff-ffffffffffff.xml"
  expect_top "$KEY_RING_VOLUME" '1654:1654 700'
  expect_top "$PROXY_VOLUME" '10001:10001 700'
  passed
}

scenario_restore_previous() {
  restore_world "a restore of an archive without .env.previous removes the server's"
  rm -f -- "$STATE/blobs/$CONTAINER/$ARCHIVE" "$COMPOSE_FOLDER/.env.previous"
  run_backup
  expect_status 0
  previous_text
  printf '%s\n' "$TEXT" > "$COMPOSE_FOLDER/.env.previous"
  empty_volumes
  run_script restore.sh --name "$ARCHIVE" --key "$KEY_FILE"
  expect_status 0
  expect_missing "$COMPOSE_FOLDER/.env.previous"
  expect_top "$KEY_RING_VOLUME" '1654:1654 700'
  passed
}

scenario_restore_wrong_key() {
  restore_world "a key that does not open the archive changes nothing and names the certificate it needs"
  empty_volumes
  cp -- "$KEYS/other-bundle.pem" "$KEY_FILE"
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "the key does not open $ARCHIVE"
  expect_output "the archive is sealed to the certificate with the serial number $(openssl x509 -in "$KEYS/recipient.crt" -noout -serial | sed 's/^serial=0*//')"
  expect_output "and the key file holds $(openssl x509 -in "$KEYS/other.crt" -noout -serial | sed 's/^serial=0*//')"
  expect_no_call "-owner "
  expect_no_call "-writer "
  expect_top "$KEY_RING_VOLUME" '0:0 755'
  [[ -f "$KEY_FILE" ]] || fail "the refused restore removed the key"
  cp -- "$KEYS/other-key.pem" "$KEY_FILE"
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "and the key file holds no certificate"
  expect_no_call "-writer "
  passed
}

seal_payload() {
  openssl cms -encrypt -binary -aes-256-gcm -in "$1" -outform DER -out "$STATE/blobs/$CONTAINER/$ARCHIVE" \
    -recip "$KEYS/recipient.crt" -keyopt rsa_padding_mode:oaep -keyopt rsa_oaep_md:sha256 || fail "cannot seal the forged archive"
}

reseal() {
  local folder="$1"
  shift
  tar -cf "$folder.tar" -C "$folder" "$@" || fail "cannot pack the forged archive"
  seal_payload "$folder.tar"
  rm -rf -- "$folder" "$folder.tar"
}

scenario_restore_forged() {
  local forgery folder="$WORK/forged"
  local -A refusal=(
    [changed-env]='.env does not match the manifest'
    [extra-member]="the archive holds 'extra', which backup.sh never writes; nothing was changed"
    [no-manifest]='the archive lacks manifest; nothing was changed'
    [not-tar]='the opened archive is not a tar archive; nothing was changed'
    [unknown-line]='of the manifest is not one backup.sh writes; nothing was changed'
  )
  for forgery in changed-env extra-member no-manifest not-tar unknown-line; do
    restore_world "an archive sealed to the right key but forged ($forgery) is refused before anything changes"
    empty_volumes
    open_archive "$STATE/blobs/$CONTAINER/$ARCHIVE" "$folder"
    case "$forgery" in
      changed-env)
        printf 'BACKUP_CONTAINER=elsewhere\n' >> "$folder/.env"
        reseal "$folder" manifest data-protection-keys.tar proxy-data.tar .env .env.previous
        ;;
      extra-member)
        printf 'stand-in\n' > "$folder/extra"
        reseal "$folder" manifest data-protection-keys.tar proxy-data.tar .env .env.previous extra
        ;;
      no-manifest) reseal "$folder" data-protection-keys.tar proxy-data.tar .env .env.previous ;;
      not-tar)
        printf 'not a tar archive\n' > "$folder.tar"
        seal_payload "$folder.tar"
        rm -rf -- "$folder" "$folder.tar"
        ;;
      unknown-line)
        printf 'surprise\n' >> "$folder/manifest"
        reseal "$folder" manifest data-protection-keys.tar proxy-data.tar .env .env.previous
        ;;
    esac
    run_script restore.sh --latest --key "$KEY_FILE"
    expect_status 1
    expect_output "${refusal[$forgery]}"
    expect_no_call "-owner "
    expect_no_call "-writer "
    expect_top "$KEY_RING_VOLUME" '0:0 755'
    expect_unchanged_files
    [[ -f "$KEY_FILE" ]] || fail "the refused restore removed the key"
  done
  passed
}

scenario_restore_empty_archive() {
  restore_world "the backup of a server whose volumes were still empty is refused, and --created-before reaches the archive before it"
  empty_volumes
  fresh_release_files
  state epoch "$(date -u -d 2026-10-31T12:00:00Z +%s)"
  run_backup
  expect_status 0
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "2026/10/20261031T120000Z.tar.p7m names no release, no data protection key and no proxy root"
  expect_no_call "-owner "
  expect_no_call "-writer "
  [[ -f "$KEY_FILE" ]] || fail "the refused restore removed the key"
  run_script restore.sh --created-before 2026-10-31T00:00:00Z --key "$KEY_FILE"
  expect_status 0
  expect_output "restored $ARCHIVE (made $NOW)"
  expect_top "$KEY_RING_VOLUME" '1654:1654 700'
  expect_top "$PROXY_VOLUME" '10001:10001 700'
  expect_unchanged_files
  expect_missing "$KEY_FILE"
  passed
}

scenario_restore_partial() {
  restore_world "a restore that cannot fill the proxy data names what it restored, keeps the key and .env, and --replace then finishes it"
  empty_volumes
  fresh_release_files
  state docker/backup/proxy-data-writer-exit 1
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "cannot fill $PROXY_VOLUME"
  expect_output "restored before the failure: $KEY_RING_VOLUME $PROXY_VOLUME (incomplete); run the restore again with --replace once its cause is fixed"
  expect_output "the key stays at $KEY_FILE"
  [[ "$(< "$COMPOSE_FOLDER/.env")" == ERP_SITE_ADDRESS=192.0.2.10* ]] || fail "the failed restore wrote .env"
  rm -f -- "$STATE/docker/backup/proxy-data-writer-exit"
  run_script restore.sh --latest --key "$KEY_FILE" --replace
  expect_status 0
  expect_top "$KEY_RING_VOLUME" '1654:1654 700'
  expect_top "$PROXY_VOLUME" '10001:10001 700'
  expect_unchanged_files
  expect_missing "$KEY_FILE"
  restore_world "a top folder whose mode the owner does not set stops the restore"
  empty_volumes
  state docker/backup/key-ring-owner-chmod-changes-nothing yes
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "after the restore the top folder of $KEY_RING_VOLUME is 1654:1654 755, not 1654:1654 700"
  expect_output "restored before the failure: $KEY_RING_VOLUME (incomplete)"
  restore_world "a volume the writer leaves short of the archive's entries stops the restore"
  empty_volumes
  state docker/backup/key-ring-writer-extracts-nothing yes
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "$KEY_RING_VOLUME holds 0 entries after the restore, not the 2 of the archive"
  passed
}

scenario_restore_choice() {
  restore_world "--latest takes the archive the storage service created last whatever its name and warns when the name gives another time, --name the one it names, and one the container lacks is refused"
  blob 2026/09/20260930T203000Z.tar.p7m 2026-09-30T20:30:00+00:00
  blob 2026/12/20261231T235959Z.tar.p7m 2026-10-01T02:00:00+00:00
  run_script restore.sh --name 2026/09/20260930T203001Z.tar.p7m --key "$KEY_FILE" --dry-run
  expect_status 1
  expect_output "$CONTAINER holds no archive 2026/09/20260930T203001Z.tar.p7m; nothing was changed"
  run_script restore.sh --latest --key "$KEY_FILE" --dry-run
  expect_status 0
  expect_output "the archive $ARCHIVE of $CONTAINER, created by the storage service at 2026-10-30T12:00:00+00:00"
  expect_no_output "gives another time than its creation"
  run_script restore.sh --name 2026/12/20261231T235959Z.tar.p7m --key "$KEY_FILE" --dry-run
  expect_status 1
  expect_output "the archive 2026/12/20261231T235959Z.tar.p7m of $CONTAINER, created by the storage service at 2026-10-01T02:00:00+00:00"
  expect_output "the name of 2026/12/20261231T235959Z.tar.p7m gives another time than its creation, more than an hour apart"
  copy_archive 2026/10/20261029T120000Z.tar.p7m 2026-10-30T13:00:00+00:00
  run_script restore.sh --latest --key "$KEY_FILE" --dry-run
  expect_status 0
  expect_output "the archive 2026/10/20261029T120000Z.tar.p7m of $CONTAINER, created by the storage service at 2026-10-30T13:00:00+00:00"
  expect_output "the name of 2026/10/20261029T120000Z.tar.p7m gives another time than its creation, more than an hour apart"
  expect_output "dry run: 2026/10/20261029T120000Z.tar.p7m opens with this key and matches its manifest"
  passed
}

scenario_restore_list() {
  restore_world "--list shows the archives by their creation, the newest first, marks a name far from its creation and leaves other blobs out"
  copy_archive 2026/10/20261029T120000Z.tar.p7m 2026-10-30T13:00:00+00:00
  blob 2026/09/20260930T203000Z.tar.p7m 2026-09-30T20:30:00+00:00
  blob notes.txt 2026-10-31T00:00:00+00:00
  run_script restore.sh --list
  expect_status 0
  [[ "$OUTPUT" == *"2026/10/20261029T120000Z.tar.p7m   created 2026-10-30T13:00:00+00:00   its name gives another time than its creation"*"$ARCHIVE   created 2026-10-30T12:00:00+00:00"*"2026/09/20260930T203000Z.tar.p7m   created 2026-09-30T20:30:00+00:00"* ]] \
    || fail "the archives are not listed by their creation, the newest first"
  expect_output "3 archive(s) in $CONTAINER, the newest creation first; 1 other blob(s) left out"
  expect_no_call "storage blob download"
  expect_no_call "flock"
  [[ -f "$KEY_FILE" ]] || fail "--list removed the key"
  run_script restore.sh --list --latest
  expect_status 1
  expect_output "--list takes no other option: it lists the archives and changes nothing"
  expect_only_calls
  passed
}

scenario_restore_created_before() {
  restore_world "--created-before takes the newest archive the storage service created before that time, and none created at it"
  copy_archive 2026/09/20260930T203000Z.tar.p7m 2026-09-30T20:30:00+00:00
  run_script restore.sh --created-before 2026-10-01T00:00:00Z --key "$KEY_FILE" --dry-run
  expect_status 0
  expect_output "the archive 2026/09/20260930T203000Z.tar.p7m of $CONTAINER, created by the storage service at 2026-09-30T20:30:00+00:00"
  expect_output "dry run: 2026/09/20260930T203000Z.tar.p7m opens with this key and matches its manifest"
  run_script restore.sh --created-before 2026-09-30T20:30:00Z --key "$KEY_FILE" --dry-run
  expect_status 1
  expect_output "$CONTAINER holds no archive created before 2026-09-30T20:30:00Z; nothing was changed"
  passed
}

scenario_restore_refusals() {
  restore_world "a restore without a key or an archive, with two choices, a malformed name or time or a file that is no key stops at once"
  run_script restore.sh --latest
  expect_status 1
  expect_output "--key <file> is required"
  expect_only_calls
  run_script restore.sh --key "$KEY_FILE"
  expect_status 1
  expect_output "choose the archive to restore: --latest, --name <archive> or --created-before <time>"
  run_script restore.sh --latest --name "$ARCHIVE" --key "$KEY_FILE"
  expect_status 1
  expect_output "choose one archive: --latest, --name <archive> or --created-before <time>, not two"
  run_script restore.sh --name "$ARCHIVE" --created-before 2026-10-30T00:00:00Z --key "$KEY_FILE"
  expect_status 1
  expect_output "choose one archive: --latest, --name <archive> or --created-before <time>, not two"
  run_script restore.sh --created-before 2026-10-30 --key "$KEY_FILE"
  expect_status 1
  expect_output "--created-before: '2026-10-30' is not a UTC time such as 2026-10-30T00:00:00Z"
  run_script restore.sh --created-before 2026-02-30T00:00:00Z --key "$KEY_FILE"
  expect_status 1
  expect_output "--created-before: '2026-02-30T00:00:00Z' is not a UTC time"
  expect_only_calls "date -u -d 2026-02-30T00:00:00Z +%s"
  run_script restore.sh --name ../escape.tar.p7m --key "$KEY_FILE"
  expect_status 1
  expect_output "is not the name of an archive"
  run_script restore.sh --latest --key "$KEYS/recipient.crt"
  expect_status 1
  expect_output "holds no private key that OpenSSL reads without a password"
  expect_only_calls "id -un"
  [[ -f "$KEYS/recipient.crt" ]] || fail "the refused restore removed the file passed as --key"
  state account runner
  run_script restore.sh --latest --key "$KEY_FILE"
  expect_status 1
  expect_output "run this as the deploy account erp-ai-pro, not as runner"
  expect_only_calls "id -un"
  passed
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  require_command bash cp mkdir mktemp grep sed date tar openssl sha256sum base64 shred stat paste
  prepare_work
  scenario_backup
  scenario_first_release
  scenario_lock_held
  scenario_failed_volume_read
  scenario_failed_upload
  scenario_existing_blob
  scenario_check_mismatch
  scenario_prune_listing_fails
  scenario_prune_delete_refused
  scenario_prune_creation_unreadable
  scenario_expiring_recipient
  scenario_certificates
  scenario_settings
  scenario_backup_refusals
  save_archive "an archive to restore"
  scenario_restore_fresh_server
  scenario_restore_dry_run
  scenario_restore_running_stack
  scenario_restore_replace
  scenario_restore_previous
  scenario_restore_wrong_key
  scenario_restore_forged
  scenario_restore_choice
  scenario_restore_list
  scenario_restore_created_before
  scenario_restore_empty_archive
  scenario_restore_partial
  scenario_restore_refusals
  printf 'backup decisions ok: %s scenarios\n' "$PASSED"
}

main "$@"
