#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/backup.sh

Backs up the server'"'"'s own files into the backup container in Azure Blob Storage: the volumes
erp-ai-pro-data-protection-keys (the key ring) and erp-ai-pro-proxy-data (the proxy'"'"'s certificate authority and
certificates), infra/compose/.env, .env.previous when it exists, and a manifest of their SHA-256 hashes with the releases
they name, the proxy root'"'"'s SHA-256 fingerprint and the key ring'"'"'s keys. erp-ai-pro-backup.timer runs it at 02:00
India time through erp-ai-pro-backup.service, as the deploy account erp-ai-pro in the deployment checkout
/srv/erp-ai-pro/ERP-AI-Pro; it holds the lock /srv/erp-ai-pro/deploy.lock while it copies the files, waiting up to 14
minutes for it.

A Microsoft BusyBox image reads each volume as its owner, read-only, without network or capabilities, into a private folder
kept in memory. OpenSSL on the server seals the archive with AES-256-GCM, its key wrapped with RSA-OAEP-SHA256 to the public
certificate secrets/erp-backup-recipient.crt, whose private key only the operator exports from Key Vault. Microsoft'"'"'s
Azure CLI image, signed in as the backup service principal (secrets/erp-backup-client.pem), uploads it as
<yyyy>/<mm>/<yyyymmddThhmmssZ>.tar.p7m without ever writing over a blob, reads its length and MD5 back and deletes the
archives older than 90 days, which the container'"'"'s write-once policy keeps until then. .env names the container and the
principal: BACKUP_BLOB_ENDPOINT, BACKUP_CONTAINER, BACKUP_AZURE_CLIENT_ID and AZURE_TENANT_ID. Each step is a line of the
journal (journalctl -u erp-ai-pro-backup.service), and a failure names the step it stopped at; nothing alerts anyone.
Runbook: docs/operations/runbooks/host-setup.md

Options:
  --help      Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/backup.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/backup.sh"

# The write-once period counts from each blob's creation on the service's clock, so a day's margin keeps this server's clock
# from asking for a delete the policy still refuses.
readonly PRUNE_AGE_SECONDS=$(( (BACKUP_IMMUTABILITY_DAYS + 1) * 86400 ))

STEP='start'
REPORTED=0
NOW=''
STAGE=''
CONTENT=''
ARCHIVE_NAME=''
UPLOADED=''
DELETED=0

finish() {
  local status=$?
  if [[ -n "$STAGE" ]]; then
    rm -rf -- "$STAGE"
  fi
  if (( status != 0 && ! REPORTED )); then
    report_failure
  fi
  exit "$status"
}

report_failure() {
  local outcome='nothing was uploaded and no archive was deleted'
  if [[ -n "$UPLOADED" ]] && (( DELETED > 0 )); then
    outcome="$UPLOADED is in the container, and $DELETED older archive(s) were deleted before it stopped"
  elif [[ -n "$UPLOADED" ]]; then
    outcome="$UPLOADED is in the container, and no older archive was deleted"
  fi
  log_error "the backup stopped at the step '$STEP': $outcome"
  REPORTED=1
}

stop() {
  report_failure
  exit 1
}

fail() {
  log_error "$*"
  stop
}

read_settings() {
  STEP='settings'
  read_backup_settings "$ENV_FILE" || stop
  require_backup_client_certificate || stop
  check_recipient_certificate "$BACKUP_RECIPIENT_FILE" || stop
  log_info "settings: the container $BACKUP_CONTAINER_NAME of $BACKUP_ENDPOINT as the service principal $BACKUP_CLIENT_ID, sealed to the certificate with $RECIPIENT_SUMMARY"
}

pull_images() {
  STEP='images'
  backup_compose pull --policy missing < /dev/null || fail "cannot pull the images of $BACKUP_COMPOSE_FILE (see the error above)"
  log_info "images: the BusyBox and Azure CLI images of $BACKUP_COMPOSE_FILE are on the server"
}

take_lock() {
  STEP='lock'
  acquire_release_lock
  log_info "lock: $RELEASE_LOCK is held, so no deployment, rollback or restore changes the files while they are read"
}

release_lock() {
  STEP='unlock'
  exec 9<&-
  log_info "unlock: the files are copied, so $RELEASE_LOCK is free again while the copies are sealed and uploaded"
}

open_stage() {
  STEP='stage'
  STAGE="$(private_stage backup)" || stop
  CONTENT="$STAGE/content"
  mkdir -- "$CONTENT" || fail "cannot create $CONTENT"
}

archive_volume() {
  local index="$1" volume="${BACKUP_VOLUMES[$1]}" part="$CONTENT/${BACKUP_VOLUME_PARTS[$1]}" members size
  STEP="volume $volume"
  backup_volume "$index" reader tar -cf - -C /volume . < /dev/null > "$part" || fail "cannot read the volume $volume (see the error above)"
  members="$(tar_members "$part")" || fail "what the reader of $volume wrote is not a tar archive"
  size="$(file_size "$part")" || fail "cannot read the size of $part"
  log_info "volume $volume: $(entry_count "$members") entries, $size bytes"
}

copy_release_files() {
  STEP='release files'
  cp -- "$ENV_FILE" "$CONTENT/$BACKUP_ENV_PART" || fail "cannot copy $ENV_FILE"
  if [[ ! -e "$PREVIOUS_ENV_FILE" ]]; then
    log_info "release files: .env, and no .env.previous, which the second deployment writes"
    return 0
  fi
  cp -- "$PREVIOUS_ENV_FILE" "$CONTENT/$BACKUP_PREVIOUS_ENV_PART" || fail "cannot copy $PREVIOUS_ENV_FILE"
  log_info "release files: .env and .env.previous"
}

release_named_by() {
  local reader="$1" file="$2" release
  if ! release="$("$reader" "$file" 2> /dev/null)"; then
    log_warn "$(basename -- "$file") names no release that deploy.sh can read, so the manifest says unreadable; the file is backed up as it is"
    printf 'unreadable'
    return 0
  fi
  printf '%s' "${release:-none}"
}

write_manifest() {
  STEP='manifest'
  local release previous root keys name part hash size key_count=0
  local -a lines=()
  release="$(release_named_by release_of_env_file "$CONTENT/$BACKUP_ENV_PART")"
  previous="$(release_named_by saved_release "$CONTENT/$BACKUP_PREVIOUS_ENV_PART")"
  root="$(proxy_root_fingerprint "$CONTENT/${BACKUP_VOLUME_PARTS[1]}")" || fail "cannot read the proxy root certificate in the proxy data"
  keys="$(key_ring_lines "$CONTENT/${BACKUP_VOLUME_PARTS[0]}")" || fail "cannot read the keys of the key ring"
  lines=(
    "$BACKUP_MANIFEST_FORMAT"
    "created $(utc_time "$NOW" '%Y-%m-%dT%H:%M:%SZ')"
    "host $(printable "$(uname -n)")"
    "release $release"
    "previous-release $previous"
    "proxy-root-sha256 $root"
  )
  if [[ -n "$keys" ]]; then
    mapfile -t -O "${#lines[@]}" lines <<< "$keys"
    key_count="$(wc -l <<< "$keys")"
  fi
  for name in "${BACKUP_VOLUME_PARTS[@]}" "$BACKUP_ENV_PART" "$BACKUP_PREVIOUS_ENV_PART"; do
    part="$CONTENT/$name"
    if [[ ! -f "$part" ]]; then
      continue
    fi
    hash="$(file_sha256 "$part")" || fail "cannot hash $name"
    size="$(file_size "$part")" || fail "cannot read the size of $name"
    lines+=("file $hash $size $name")
  done
  printf '%s\n' "${lines[@]}" > "$CONTENT/$BACKUP_MANIFEST" || fail "cannot write the manifest"
  log_info "manifest: release ${release%% *}, ${key_count// /} data protection key(s), proxy root SHA-256 fingerprint $root"
}

seal_archive() {
  STEP='seal'
  local plain="$STAGE/archive.tar" size
  local -a parts=("$BACKUP_MANIFEST" "${BACKUP_VOLUME_PARTS[@]}" "$BACKUP_ENV_PART")
  if [[ -f "$CONTENT/$BACKUP_PREVIOUS_ENV_PART" ]]; then
    parts+=("$BACKUP_PREVIOUS_ENV_PART")
  fi
  BACKUP_ARCHIVE="$STAGE/archive.tar.p7m"
  tar -cf "$plain" -C "$CONTENT" "${parts[@]}" || fail "cannot pack the archive"
  seal_file "$plain" "$BACKUP_ARCHIVE" || fail "OpenSSL cannot seal the archive (see its error above)"
  rm -f -- "$plain"
  # The Azure CLI container runs as 1654 and reads the sealed archive alone through a read-only mount; the folder stays private.
  chmod 644 -- "$BACKUP_ARCHIVE" || fail "cannot let the Azure CLI container read $BACKUP_ARCHIVE"
  size="$(file_size "$BACKUP_ARCHIVE")" || fail "cannot read the size of $BACKUP_ARCHIVE"
  log_info "seal: $size bytes of AES-256-GCM, its key wrapped with RSA-OAEP-SHA256 for the certificate with serial $RECIPIENT_SERIAL"
}

upload_archive() {
  STEP='upload'
  ARCHIVE_NAME="$(archive_name_of "$NOW")" || fail "cannot name the archive"
  backup_blob upload --name "$ARCHIVE_NAME" --file "$BACKUP_UPLOAD_PATH" --overwrite false --validate-content --no-progress \
    --output none || fail "cannot upload $ARCHIVE_NAME into $BACKUP_CONTAINER_NAME (see the Azure CLI's error above)"
  UPLOADED="$BACKUP_CONTAINER_NAME/$ARCHIVE_NAME"
  log_info "upload: $UPLOADED"
}

check_upload() {
  STEP='check'
  local length md5 expected_length expected_md5
  blob_properties "$ARCHIVE_NAME" '[properties.contentLength, properties.contentSettings.contentMd5]' \
    || fail "cannot read $UPLOADED back (see the Azure CLI's error above)"
  length="${BLOB_PROPERTIES[0]:-}" md5="${BLOB_PROPERTIES[1]:-}"
  expected_length="$(file_size "$BACKUP_ARCHIVE")" || fail "cannot read the size of $BACKUP_ARCHIVE"
  expected_md5="$(file_md5_base64 "$BACKUP_ARCHIVE")" || fail "cannot hash $BACKUP_ARCHIVE"
  if [[ "$length" != "$expected_length" || "$md5" != "$expected_md5" ]]; then
    fail "$UPLOADED reads back as $(printable "${length:-nothing}") bytes with the MD5 $(printable "${md5:-nothing}"), not the $expected_length bytes with the MD5 $expected_md5 that were sealed"
  fi
  log_info "check: $UPLOADED holds the $length bytes that were sealed, MD5 $md5"
}

prune_archives() {
  STEP='prune'
  local listing name created since cutoff kept=0 others=0
  local -a expired=()
  local -A seen=() created_of=()
  listing="$(backup_blob list --num-results '*' --query '[].[name, properties.creationTime]' --output tsv)" \
    || fail "cannot list the archives of $BACKUP_CONTAINER_NAME (see the Azure CLI's error above)"
  cutoff=$(( NOW - PRUNE_AGE_SECONDS ))
  while IFS=$'\t' read -r name created; do
    created="${created%$'\r'}"
    if [[ -z "$name" ]]; then
      continue
    fi
    if [[ ! "$name" =~ $BACKUP_ARCHIVE_PATTERN ]]; then
      others=$(( others + 1 ))
      continue
    fi
    [[ -z "${seen[$name]:-}" ]] || fail "$(listed_twice "$name")"
    seen[$name]=1
    since="$(creation_epoch "$created")" \
      || fail "the archive $(printable "$name") reports the creation time '$(printable "$created")', which is not a time"
    if (( since >= cutoff )); then
      kept=$(( kept + 1 ))
      continue
    fi
    expired+=("$name")
    created_of[$name]="$created"
  done <<< "$listing"
  for name in "${expired[@]}"; do
    backup_blob delete --name "$name" --output none \
      || fail "cannot delete $name, older than $BACKUP_IMMUTABILITY_DAYS days (see the Azure CLI's error above)"
    DELETED=$(( DELETED + 1 ))
    log_info "prune: deleted $name, created $(printable "${created_of[$name]}")"
  done
  log_info "prune: deleted $DELETED archive(s) older than $BACKUP_IMMUTABILITY_DAYS days, kept $kept, left $others other blob(s) alone"
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '$(printable "${ARGS[0]}")' (see --help)"
  require_command docker flock openssl tar sha256sum base64 date mktemp uname
  require_release_host
  umask 077
  trap finish EXIT
  trap 'exit 129' HUP
  trap 'exit 130' INT
  trap 'exit 143' TERM
  NOW="$(date -u +%s)"
  [[ "$NOW" =~ ^[0-9]{10,}$ ]] || fail "cannot read this server's clock"
  read_settings
  pull_images
  take_lock
  open_stage
  archive_volume 0
  archive_volume 1
  copy_release_files
  release_lock
  write_manifest
  seal_archive
  upload_archive
  check_upload
  prune_archives
  printf "backed up the server's files to %s; %s archive(s) older than %s days deleted\n" "$UPLOADED" "$DELETED" "$BACKUP_IMMUTABILITY_DAYS"
}

main "$@"
