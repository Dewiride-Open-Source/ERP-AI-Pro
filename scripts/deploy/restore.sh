#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/restore.sh (--latest | --name <archive> | --created-before <time>) --key <file> [--replace] [--dry-run]
       bash scripts/deploy/restore.sh --list

Restores an archive that backup.sh made into this server: the volumes erp-ai-pro-data-protection-keys and
erp-ai-pro-proxy-data, infra/compose/.env, and .env.previous as the archive holds it. It runs as the deploy account erp-ai-pro
in the deployment checkout /srv/erp-ai-pro/ERP-AI-Pro, with the stack stopped (sudo systemctl stop erp-ai-pro.service): it
refuses while a container of the stack runs, and holds the lock /srv/erp-ai-pro/deploy.lock, so neither the start at boot
nor a deployment runs beside it. Restore before the API first starts on a rebuilt server, or the API makes a new key ring.

It downloads the archive as the backup service principal into a private folder kept in memory, opens it with the private
key of the certificate it was sealed to, which the operator exports for the restore alone
(export-client-certificate.sh backup-recipient-key) and copies to this server, and checks every file against the manifest
before it changes anything. Then it gives each volume'"'"'s top folder the owner and mode its image gives it (1654:1654 for
the key ring, 10001:10001 for the proxy data, both 0700), fills the volume as that owner, and writes .env and .env.previous
(0600) as the archive holds them. The key never stays: a restore shreds the key file once it is done, and a dry run or a
failure says to. An archive whose manifest names no release, no key and no proxy root, the backup of a server whose
volumes were still empty, is refused before anything changes. Runbook: docs/operations/runbooks/restore.md

Options:
  --list                      List every archive of the backup container with the time the storage service created it,
                              newest first, marking a name whose time lies more than an hour from its creation; it needs
                              no key, changes nothing and may run while the stack runs.
  --latest                    Restore the archive the storage service created last.
  --name <archive>            Restore this archive, such as 2026/10/20261030T203000Z.tar.p7m.
  --created-before <time>     Restore the newest archive the storage service created before this UTC time, such as
                              2026-10-30T00:00:00Z: after a suspected compromise, a time before it began.
  --key <file>                The exported private key with its certificate (PEM), on this server.
  --replace                   Replace the files of volumes that hold some; without it only empty volumes are filled.
  --dry-run                   Download, open and check the archive and list what a restore would change, changing
                              nothing; it may run while the stack runs.
  --help                      Show this help.'

# shellcheck source=lib/backup.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/backup.sh"

readonly ARCHIVE_TIME_TOLERANCE_SECONDS=3600
readonly UTC_TIME_PATTERN='^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]Z$'

LIST=0
LATEST=0
REPLACE=0
WANTED=''
CREATED_BEFORE=''
BEFORE_EPOCH=0
STAGE=''
CONTENT=''
ARCHIVE_NAME=''
ARCHIVE_CREATED_EPOCH=0
STACK_RUNNING=''
MANIFEST_CREATED=''
MANIFEST_HOST=''
MANIFEST_RELEASE=''
MANIFEST_PREVIOUS=''
MANIFEST_ROOT=''
MANIFEST_KEYS=0
RESTORED=()
declare -A VOLUME_STATE=()
declare -A VOLUME_ENTRIES=()
declare -A ARCHIVE_ENTRIES=()

finish() {
  local status=$?
  if [[ -n "$STAGE" ]]; then
    rm -rf -- "$STAGE"
  fi
  if (( status != 0 && ${#RESTORED[@]} > 0 )); then
    log_error "restored before the failure: ${RESTORED[*]}; run the restore again with --replace once its cause is fixed"
  fi
  warn_key_left
  exit "$status"
}

set_restore_option() {
  [[ -n "$2" ]] || die "$1 requires a value"
  case "$1" in
    --key) KEY_FILE="$2" ;;
    --name) WANTED="$2" ;;
    --created-before) CREATED_BEFORE="$2" ;;
  esac
}

parse_restore_args() {
  local -a rest=()
  local choices=0
  while (( $# > 0 )); do
    case "$1" in
      --key | --name | --created-before)
        set_restore_option "$1" "${2:-}"
        shift
        ;;
      --key=* | --name=* | --created-before=*) set_restore_option "${1%%=*}" "${1#*=}" ;;
      --list) LIST=1 ;;
      --latest) LATEST=1 ;;
      --replace) REPLACE=1 ;;
      *) rest+=("$1") ;;
    esac
    shift
  done
  parse_args "${rest[@]}"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '$(printable "${ARGS[0]}")' (see --help)"
  if (( LIST )); then
    if (( LATEST || REPLACE || DRY_RUN )) || [[ -n "$WANTED$CREATED_BEFORE$KEY_FILE" ]]; then
      die "--list takes no other option: it lists the archives and changes nothing (see --help)"
    fi
    return 0
  fi
  choices="$LATEST"
  if [[ -n "$WANTED" ]]; then
    choices=$(( choices + 1 ))
  fi
  if [[ -n "$CREATED_BEFORE" ]]; then
    choices=$(( choices + 1 ))
  fi
  if (( choices > 1 )); then
    die "choose one archive: --latest, --name <archive> or --created-before <time>, not two"
  fi
  if (( choices == 0 )); then
    die "choose the archive to restore: --latest, --name <archive> or --created-before <time> (see --help)"
  fi
  if [[ -n "$WANTED" && ! "$WANTED" =~ $BACKUP_ARCHIVE_PATTERN ]]; then
    die "--name: '$(printable "$WANTED")' is not the name of an archive, such as 2026/10/20261030T203000Z.tar.p7m"
  fi
  if [[ -n "$CREATED_BEFORE" ]]; then
    if [[ ! "$CREATED_BEFORE" =~ $UTC_TIME_PATTERN ]] || ! BEFORE_EPOCH="$(creation_epoch "$CREATED_BEFORE")"; then
      die "--created-before: '$(printable "$CREATED_BEFORE")' is not a UTC time such as 2026-10-30T00:00:00Z"
    fi
  fi
  [[ -n "$KEY_FILE" ]] || die "--key <file> is required: the private key that opens the archive (see --help)"
}

running_stack() {
  local running service
  local -a services=()
  running="$(host_docker ps --filter "label=$BACKUP_STACK_PROJECT_LABEL" --format '{{.Label "com.docker.compose.service"}}')" \
    || die "cannot list the running containers of the stack; nothing was changed"
  while IFS= read -r service; do
    if [[ -n "$service" ]]; then
      services+=("$(printable "$service")")
    fi
  done <<< "$running"
  STACK_RUNNING="${services[*]}"
}

archive_listing() {
  backup_blob list --num-results '*' --query '[].[name, properties.creationTime]' --output tsv \
    || die "cannot list the archives of $BACKUP_CONTAINER_NAME (see the Azure CLI's error above); nothing was changed"
}

archive_creation() {
  local name="$1" created="$2" since
  if ! since="$(creation_epoch "$created")"; then
    die "the archive $(printable "$name") reports the creation time '$(printable "$created")', which is not a time; nothing was changed"
  fi
  printf '%s' "$since"
}

name_disagrees() {
  local named
  named="$(named_epoch "$1")" || return 0
  (( named - $2 > ARCHIVE_TIME_TOLERANCE_SECONDS || $2 - named > ARCHIVE_TIME_TOLERANCE_SECONDS ))
}

list_archives() {
  local listing name created since mark others=0
  local -a rows=()
  local -A seen=()
  listing="$(archive_listing)"
  while IFS=$'\t' read -r name created; do
    created="${created%$'\r'}"
    if [[ -z "$name" ]]; then
      continue
    fi
    if [[ ! "$name" =~ $BACKUP_ARCHIVE_PATTERN ]]; then
      others=$(( others + 1 ))
      continue
    fi
    [[ -z "${seen[$name]:-}" ]] || die "$(listed_twice "$name"); nothing was listed"
    seen[$name]=1
    since="$(archive_creation "$name" "$created")"
    mark=''
    if name_disagrees "$name" "$since"; then
      mark='   its name gives another time than its creation'
    fi
    rows+=("$since"$'\t'"$name   created $(printable "$created")$mark")
  done <<< "$listing"
  if (( ${#rows[@]} > 0 )); then
    printf '%s\n' "${rows[@]}" | sort -t $'\t' -k1,1nr | cut -f2-
  fi
  printf '%s archive(s) in %s, the newest creation first; %s other blob(s) left out\n' "${#rows[@]}" "$BACKUP_CONTAINER_NAME" "$others"
}

choose_archive() {
  local listing name created since newest=-1 chosen=''
  local -A seen=()
  listing="$(archive_listing)"
  # Whoever holds the backup principal's certificate chooses an archive's name, so the newest archive is the one the storage
  # service created last, and a name whose time lies far from that creation is reported.
  while IFS=$'\t' read -r name created; do
    created="${created%$'\r'}"
    if [[ ! "$name" =~ $BACKUP_ARCHIVE_PATTERN ]]; then
      continue
    fi
    [[ -z "${seen[$name]:-}" ]] || die "$(listed_twice "$name"); nothing was changed"
    seen[$name]=1
    if [[ -n "$WANTED" && "$name" != "$WANTED" ]]; then
      continue
    fi
    since="$(archive_creation "$name" "$created")"
    if [[ -n "$CREATED_BEFORE" ]] && (( since >= BEFORE_EPOCH )); then
      continue
    fi
    if (( since > newest )); then
      ARCHIVE_NAME="$name"
      chosen="$created"
      newest="$since"
    fi
  done <<< "$listing"
  if [[ -z "$ARCHIVE_NAME" && -n "$WANTED" ]]; then
    die "$BACKUP_CONTAINER_NAME holds no archive $WANTED; nothing was changed"
  fi
  if [[ -z "$ARCHIVE_NAME" && -n "$CREATED_BEFORE" ]]; then
    die "$BACKUP_CONTAINER_NAME holds no archive created before $CREATED_BEFORE; nothing was changed"
  fi
  [[ -n "$ARCHIVE_NAME" ]] || die "$BACKUP_CONTAINER_NAME holds no archive yet; nothing was changed"
  ARCHIVE_CREATED_EPOCH="$newest"
  log_info "the archive $ARCHIVE_NAME of $BACKUP_CONTAINER_NAME, created by the storage service at $(printable "$chosen")"
  if name_disagrees "$ARCHIVE_NAME" "$newest"; then
    log_warn "the name of $ARCHIVE_NAME gives another time than its creation, more than an hour apart: backup.sh names an archive in the minute it uploads it, so make sure this one is a backup of this server's before it is restored (docs/operations/runbooks/restore.md)"
  fi
}

download_archive() {
  local length md5 created since sealed="$STAGE/archive.tar.p7m"
  blob_properties "$ARCHIVE_NAME" '[properties.contentLength, properties.contentSettings.contentMd5, properties.creationTime]' \
    || die "cannot read the properties of $ARCHIVE_NAME (see the Azure CLI's error above); nothing was changed"
  length="${BLOB_PROPERTIES[0]:-}" md5="${BLOB_PROPERTIES[1]:-}" created="${BLOB_PROPERTIES[2]:-}"
  since="$(creation_epoch "$created")" || since=''
  if [[ "$since" != "$ARCHIVE_CREATED_EPOCH" ]]; then
    die "the storage service reports $ARCHIVE_NAME created at $(printable "$created"), not at the time its listing gave; nothing was changed"
  fi
  backup_blob download --name "$ARCHIVE_NAME" --no-progress --output none > "$sealed" \
    || die "cannot download $ARCHIVE_NAME (see the Azure CLI's error above); nothing was changed"
  if [[ "$(file_size "$sealed")" != "$length" || "$(file_md5_base64 "$sealed")" != "$md5" ]]; then
    die "the download of $ARCHIVE_NAME does not match its length $(printable "$length") and MD5 $(printable "$md5") in the container; nothing was changed"
  fi
  log_info "downloaded $length bytes, MD5 $md5"
}

open_archive() {
  local sealed="$STAGE/archive.tar.p7m" plain="$STAGE/archive.tar" members member
  local -A seen=()
  if ! open_sealed "$sealed" "$plain"; then
    rm -f -- "$plain"
    die "the key does not open $ARCHIVE_NAME, or the archive was changed: $(key_mismatch_hint "$sealed"); nothing was changed"
  fi
  members="$(tar_members "$plain")" || die "the opened archive is not a tar archive; nothing was changed"
  while IFS= read -r member; do
    case "$member" in
      "$BACKUP_MANIFEST" | "${BACKUP_VOLUME_PARTS[0]}" | "${BACKUP_VOLUME_PARTS[1]}" | "$BACKUP_ENV_PART" | "$BACKUP_PREVIOUS_ENV_PART") ;;
      *) die "the archive holds '$(printable "$member")', which backup.sh never writes; nothing was changed" ;;
    esac
    [[ -z "${seen[$member]:-}" ]] || die "the archive holds $member twice; nothing was changed"
    seen[$member]=1
  done <<< "$members"
  for member in "$BACKUP_MANIFEST" "${BACKUP_VOLUME_PARTS[@]}" "$BACKUP_ENV_PART"; do
    [[ -n "${seen[$member]:-}" ]] || die "the archive lacks $member; nothing was changed"
  done
  mkdir -- "$CONTENT" || die "cannot create $CONTENT"
  tar -xf "$plain" -C "$CONTENT" || die "cannot unpack the opened archive; nothing was changed"
  rm -f -- "$plain"
  for member in "${!seen[@]}"; do
    [[ -f "$CONTENT/$member" && ! -L "$CONTENT/$member" ]] || die "$member of the archive is not a regular file; nothing was changed"
  done
}

check_manifest() {
  local manifest="$CONTENT/$BACKUP_MANIFEST" line number=0 hash size name actual
  local file_line='^file ([0-9a-f]{64}) ([0-9]{1,12}) (data-protection-keys\.tar|proxy-data\.tar|\.env|\.env\.previous)$'
  local -A listed=()
  while IFS= read -r line || [[ -n "$line" ]]; do
    number=$(( number + 1 ))
    if (( number == 1 )); then
      [[ "$line" == "$BACKUP_MANIFEST_FORMAT" ]] || die "the manifest begins '$(printable "$line")', not '$BACKUP_MANIFEST_FORMAT'; nothing was changed"
      continue
    fi
    case "$line" in
      'created '*) MANIFEST_CREATED="$(printable "${line#created }")" ;;
      'host '*) MANIFEST_HOST="$(printable "${line#host }")" ;;
      'release '*) MANIFEST_RELEASE="$(printable "${line#release }")" ;;
      'previous-release '*) MANIFEST_PREVIOUS="$(printable "${line#previous-release }")" ;;
      'proxy-root-sha256 '*) MANIFEST_ROOT="$(printable "${line#proxy-root-sha256 }")" ;;
      'data-protection-key '*) MANIFEST_KEYS=$(( MANIFEST_KEYS + 1 )) ;;
      'file '*)
        [[ "$line" =~ $file_line ]] || die "line $number of the manifest is not 'file <sha256> <bytes> <name>'; nothing was changed"
        hash="${BASH_REMATCH[1]}"
        size="${BASH_REMATCH[2]}"
        name="${BASH_REMATCH[3]}"
        [[ -z "${listed[$name]:-}" ]] || die "the manifest names $name twice; nothing was changed"
        listed[$name]=1
        [[ -f "$CONTENT/$name" ]] || die "the manifest names $name, which the archive lacks; nothing was changed"
        actual="$(file_sha256 "$CONTENT/$name")" || die "cannot hash $name"
        if [[ "$actual" != "$hash" || "$(file_size "$CONTENT/$name")" != "$size" ]]; then
          die "$name does not match the manifest, which gives it the SHA-256 $hash and $size bytes; nothing was changed"
        fi
        ;;
      *) die "line $number of the manifest is not one backup.sh writes; nothing was changed" ;;
    esac
  done < "$manifest"
  for name in "${BACKUP_VOLUME_PARTS[@]}" "$BACKUP_ENV_PART" "$BACKUP_PREVIOUS_ENV_PART"; do
    if [[ -f "$CONTENT/$name" && -z "${listed[$name]:-}" ]]; then
      die "the archive holds $name, which its manifest does not name; nothing was changed"
    fi
  done
  log_info "every file matches the manifest of $MANIFEST_CREATED from ${MANIFEST_HOST:-an unnamed host}: release ${MANIFEST_RELEASE%% *} (before it ${MANIFEST_PREVIOUS%% *}), $MANIFEST_KEYS data protection key(s), proxy root SHA-256 fingerprint ${MANIFEST_ROOT:-none}"
}

refuse_empty_archive() {
  if [[ "${MANIFEST_RELEASE%% *}" == none && "${MANIFEST_ROOT:-none}" == none ]] && (( MANIFEST_KEYS == 0 )); then
    die "$ARCHIVE_NAME names no release, no data protection key and no proxy root: it is the backup of a server whose volumes were still empty, made $MANIFEST_CREATED on ${MANIFEST_HOST:-an unnamed host}, which restores nothing; choose an archive made before (--list, then --name or --created-before); nothing was changed"
  fi
}

read_volumes() {
  local index volume members
  for index in "${!BACKUP_VOLUMES[@]}"; do
    volume="${BACKUP_VOLUMES[$index]}"
    host_docker volume inspect --format '{{.Name}}' "$volume" > /dev/null \
      || die "the volume $volume does not exist; bootstrap.sh creates it; nothing was changed"
    VOLUME_STATE[$index]="$(backup_volume "$index" reader stat -c '%u:%g %a' /volume < /dev/null)" \
      || die "cannot read the top folder of $volume (see the error above); nothing was changed"
    members="$(backup_volume "$index" reader find /volume -mindepth 1 -maxdepth 1 < /dev/null)" \
      || die "cannot list $volume (see the error above); nothing was changed"
    VOLUME_ENTRIES[$index]="$(entry_count "$members")"
    members="$(tar_members "$CONTENT/${BACKUP_VOLUME_PARTS[$index]}")" || die "cannot list ${BACKUP_VOLUME_PARTS[$index]}"
    ARCHIVE_ENTRIES[$index]="$(entry_count "$members")"
    log_info "the volume $volume holds ${VOLUME_ENTRIES[$index]} top-level entries, top folder ${VOLUME_STATE[$index]}; the archive holds ${ARCHIVE_ENTRIES[$index]} entries for it"
  done
}

volumes_holding_files() {
  local index
  local -a holding=()
  for index in "${!BACKUP_VOLUMES[@]}"; do
    if (( VOLUME_ENTRIES[$index] > 0 )); then
      holding+=("${BACKUP_VOLUMES[$index]}")
    fi
  done
  printf '%s' "${holding[*]}"
}

restore_volume() {
  local index="$1" volume="${BACKUP_VOLUMES[$1]}" owner="${BACKUP_VOLUME_OWNERS[$1]}" expected state members restored
  expected="$owner $BACKUP_VOLUME_MODE"
  RESTORED+=("$volume (incomplete)")
  # Docker gives an empty volume its image folder's owner and mode only when a container first mounts it, so on a rebuilt
  # server the top folder is still root's and the owner's tar could not write into it.
  if [[ "${VOLUME_STATE[$index]}" != "$expected" ]]; then
    backup_volume "$index" owner chown "$owner" /volume < /dev/null || die "cannot give the top folder of $volume to $owner"
    backup_volume "$index" owner chmod "$BACKUP_VOLUME_MODE" /volume < /dev/null || die "cannot set the mode of the top folder of $volume"
  fi
  if (( VOLUME_ENTRIES[$index] > 0 )); then
    backup_volume "$index" writer find /volume -mindepth 1 -delete < /dev/null || die "cannot empty $volume (see the error above)"
  fi
  backup_volume "$index" writer tar -xof - -C /volume < "$CONTENT/${BACKUP_VOLUME_PARTS[$index]}" \
    || die "cannot fill $volume (see the error above)"
  state="$(backup_volume "$index" reader stat -c '%u:%g %a' /volume < /dev/null)" || die "cannot read the top folder of $volume"
  [[ "$state" == "$expected" ]] || die "after the restore the top folder of $volume is $(printable "$state"), not $expected"
  members="$(backup_volume "$index" reader find /volume -mindepth 1 < /dev/null)" || die "cannot list $volume"
  restored="$(entry_count "$members")"
  (( restored == ARCHIVE_ENTRIES[$index] )) || die "$volume holds $restored entries after the restore, not the ${ARCHIVE_ENTRIES[$index]} of the archive"
  RESTORED[${#RESTORED[@]} - 1]="$volume"
  log_info "restored the volume $volume: $restored entries, top folder $expected"
}

restore_release_files() {
  local before replaced
  if before="$(release_of_env_file "$ENV_FILE" 2> /dev/null)"; then
    replaced="${before%% *}"
    replaced="${replaced:-none}"
  else
    replaced='unreadable'
  fi
  install_file_atomically "$CONTENT/$BACKUP_ENV_PART" "$ENV_FILE" || die "cannot write $ENV_FILE"
  RESTORED+=(.env)
  if [[ -f "$CONTENT/$BACKUP_PREVIOUS_ENV_PART" ]]; then
    install_file_atomically "$CONTENT/$BACKUP_PREVIOUS_ENV_PART" "$PREVIOUS_ENV_FILE" || die "cannot write $PREVIOUS_ENV_FILE"
    RESTORED+=(.env.previous)
  elif [[ -e "$PREVIOUS_ENV_FILE" ]]; then
    rm -f -- "$PREVIOUS_ENV_FILE" || die "cannot remove $PREVIOUS_ENV_FILE, which the archive lacks"
    RESTORED+=("no .env.previous")
  fi
  log_info ".env names the release ${MANIFEST_RELEASE%% *} of the archive; the replaced .env named $replaced"
}

main() {
  parse_restore_args "$@"
  require_command docker flock openssl tar sha256sum base64 date mktemp shred sort cut
  require_release_host
  umask 077
  if (( LIST )); then
    read_backup_settings "$ENV_FILE" || die "nothing was listed"
    require_backup_client_certificate || die "nothing was listed"
    backup_compose pull --policy missing < /dev/null || die "cannot pull the images of $BACKUP_COMPOSE_FILE"
    list_archives
    exit 0
  fi
  trap finish EXIT
  trap 'exit 129' HUP
  trap 'exit 130' INT
  trap 'exit 143' TERM
  check_key
  if (( ! DRY_RUN )); then
    acquire_release_lock
  fi
  running_stack
  if [[ -n "$STACK_RUNNING" ]] && (( ! DRY_RUN )); then
    die "the stack runs ($STACK_RUNNING); stop it first (sudo systemctl stop erp-ai-pro.service), so no container uses the volumes or .env while they are restored; nothing was changed"
  fi
  read_backup_settings "$ENV_FILE" || die "nothing was changed"
  require_backup_client_certificate || die "nothing was changed"
  backup_compose pull --policy missing < /dev/null || die "cannot pull the images of $BACKUP_COMPOSE_FILE; nothing was changed"
  STAGE="$(private_stage restore)" || die "nothing was changed"
  CONTENT="$STAGE/content"
  choose_archive
  download_archive
  open_archive
  check_manifest
  refuse_empty_archive
  read_volumes

  local holding release_files='.env'
  holding="$(volumes_holding_files)"
  if [[ -f "$CONTENT/$BACKUP_PREVIOUS_ENV_PART" ]]; then
    release_files='.env and .env.previous'
  fi
  if (( DRY_RUN )); then
    if [[ -n "$STACK_RUNNING" ]]; then
      log_info "the stack runs ($STACK_RUNNING); a restore needs it stopped first"
    fi
    if [[ -n "$holding" ]] && (( ! REPLACE )); then
      log_info "$holding hold files, so a restore needs --replace"
    fi
    printf 'dry run: %s opens with this key and matches its manifest; a restore would fill %s and write %s; nothing was changed\n' \
      "$ARCHIVE_NAME" "${BACKUP_VOLUMES[*]}" "$release_files"
    exit 0
  fi
  if [[ -n "$holding" ]] && (( ! REPLACE )); then
    die "$holding hold files, which a restore replaces only with --replace; nothing was changed"
  fi

  local index
  for index in "${!BACKUP_VOLUMES[@]}"; do
    restore_volume "$index"
  done
  restore_release_files
  shred_key
  printf 'restored %s (made %s): the key ring and the proxy data (root SHA-256 fingerprint %s) and .env, which names the release %s; start the stack when the checkout is at that release, otherwise deploy the release that ran last (docs/operations/runbooks/restore.md)\n' \
    "$ARCHIVE_NAME" "$MANIFEST_CREATED" "${MANIFEST_ROOT:-none}" "${MANIFEST_RELEASE%% *}"
}

main "$@"
