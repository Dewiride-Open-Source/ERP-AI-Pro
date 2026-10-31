#!/usr/bin/env bash

USAGE='Usage: bash scripts/deploy/journal.sh
       bash scripts/deploy/journal.sh --list
       bash scripts/deploy/journal.sh --fetch <first yyyy-mm-dd> [--through <last yyyy-mm-dd>] --key <file> [--output <folder>]

Copies the server'"'"'s journal, one India day (00:00 to 24:00 India time) at a time, into the write-once container
host-journal of the backup account in Azure Blob Storage, which keeps each copy 365 days. erp-ai-pro-journal.timer runs it
at 02:30 India time through erp-ai-pro-journal.service, as the deploy account erp-ai-pro with the group systemd-journal,
which reads the whole journal, in the deployment checkout /srv/erp-ai-pro/ERP-AI-Pro. It takes no lock: it reads the
journal alone.

A run copies each of the last 30 days up to yesterday, the oldest first, of which the container holds no copy and the
journal holds entries, so it catches up the nights the server was off. journalctl --output=json --all writes the day'"'"'s
entries as one JSON object per line with every field whole, where without --all it would write a field of 4096 bytes or
more, such as a stack trace, as null, from the day'"'"'s first microsecond to its last in UTC (18:30 UTC the day before to
18:30 UTC); gzip compresses them, and OpenSSL on the server seals them as backup.sh seals its archives: AES-256-GCM, its
key wrapped with RSA-OAEP-SHA256 to the public certificate secrets/erp-backup-recipient.crt, whose private key only the
operator exports from Key Vault. Microsoft'"'"'s Azure CLI image, signed in as the backup service principal
(secrets/erp-backup-client.pem), uploads the copy as <yyyy>/<mm>/<yyyymmdd>.json.gz.p7m without ever writing over a blob
and reads its length and MD5 back. Last, it deletes the copies the storage service created more than 366 days ago, which
the container'"'"'s write-once policy keeps for 365. .env names the backup account and the principal: BACKUP_BLOB_ENDPOINT,
BACKUP_AZURE_CLIENT_ID and AZURE_TENANT_ID. Each step is a line of the journal (journalctl -u erp-ai-pro-journal.service),
and a failure names the step it stopped at; nothing alerts anyone.
Runbook: docs/operations/runbooks/host-setup.md

Options:
  --list                        List every copy of the container with the time the storage service created it, newest
                                first, marking a copy created before its day ended; it needs no key and changes nothing.
  --fetch <first yyyy-mm-dd>    Download the copy of each India day from this one through --through, the oldest first,
                                check it against the container'"'"'s record, that the storage service created it after its
                                day ended and, once the key opens it, that every entry lies within the day, and write the
                                day'"'"'s JSON lines to its own file journal-<yyyy-mm-dd>.json (0600) in the output folder
                                (0700). The key never stays: a fetch shreds the key file once the last day is written, and
                                a failure says to, naming the days it wrote.
  --through <last yyyy-mm-dd>   The last day of the period, which includes it: at most 31 days in all, each of them over.
                                Without it the period is the --fetch day alone.
  --key <file>                  The exported private key with its certificate (PEM), on this server.
  --output <folder>             Where --fetch writes the days, a folder that does not exist yet (default:
                                /srv/erp-ai-pro/journal-<first yyyy-mm-dd>-to-<last yyyy-mm-dd>).
  --help                        Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/backup.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/backup.sh"

readonly COPY_WINDOW_DAYS=30
readonly DAY_SECONDS=86400
readonly INDIA_OFFSET_SECONDS=$(( INDIA_OFFSET_MINUTES * 60 ))
readonly JOURNAL_GROUP='systemd-journal'
readonly DATE_PATTERN='^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$'
# The write-once period counts from each copy's creation on the service's clock, so a day's margin keeps this server's clock
# from asking for a delete the policy still refuses.
readonly PRUNE_AGE_SECONDS=$(( (JOURNAL_IMMUTABILITY_DAYS + 1) * DAY_SECONDS ))
# Whoever holds the backup principal's certificate chooses a copy's name, so a copy the storage service created more than
# this before its day ended was made before the day was over and cannot hold it.
readonly COPY_TIME_TOLERANCE_SECONDS=3600
readonly FETCH_MAX_DAYS=31

MODE=copy
LIST=0
FETCH_DATE=''
THROUGH_DATE=''
OUTPUT=''
OUTPUT_CREATED=0
WRITTEN=()
STEP='start'
REPORTED=0
NOW=0
STAGE=''
DAY_DATE=''
DAY_NAME=''
DAY_START=0
DAY_END=0
DAY_SINCE=''
DAY_UNTIL=''
ALREADY=0
EMPTY=0
DELETED=0
OTHERS=0
UPLOADED=()
PREMADE=()
declare -A CREATED_OF=()
declare -A CREATED_TEXT=()

finish() {
  local status=$?
  if [[ -n "$STAGE" ]]; then
    rm -rf -- "$STAGE"
  fi
  if (( status != 0 && ! REPORTED )) && [[ "$MODE" == copy ]]; then
    report_failure
  fi
  if (( status != 0 )) && [[ "$MODE" == fetch ]]; then
    report_fetch_failure
  fi
  warn_key_left
  exit "$status"
}

report_failure() {
  local outcome='nothing was uploaded'
  if (( ${#UPLOADED[@]} > 0 )); then
    outcome="the copies of ${UPLOADED[*]} are in the container"
  fi
  if (( DELETED > 0 )); then
    outcome+=", and $DELETED older copy(ies) were deleted before it stopped"
  elif (( ${#UPLOADED[@]} > 0 )); then
    outcome+=', and no older copy was deleted'
  else
    outcome+=' and no copy was deleted'
  fi
  log_error "the journal copy stopped at the step '$STEP': $outcome"
  REPORTED=1
}

stop() {
  if [[ "$MODE" == copy ]]; then
    report_failure
  fi
  exit 1
}

fail() {
  log_error "$*"
  stop
}

set_journal_option() {
  [[ -n "$2" ]] || die "$1 requires a value"
  case "$1" in
    --fetch) FETCH_DATE="$2" ;;
    --through) THROUGH_DATE="$2" ;;
    --key) KEY_FILE="$2" ;;
    --output) OUTPUT="$2" ;;
  esac
}

parse_journal_args() {
  local -a rest=()
  while (( $# > 0 )); do
    case "$1" in
      --fetch | --through | --key | --output)
        set_journal_option "$1" "${2:-}"
        shift
        ;;
      --fetch=* | --through=* | --key=* | --output=*) set_journal_option "${1%%=*}" "${1#*=}" ;;
      --list) LIST=1 ;;
      *) rest+=("$1") ;;
    esac
    shift
  done
  parse_args "${rest[@]}"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '$(printable "${ARGS[0]}")' (see --help)"
  if (( LIST )); then
    [[ -z "$FETCH_DATE$THROUGH_DATE$KEY_FILE$OUTPUT" ]] || die "--list takes no other option: it lists the copies and changes nothing (see --help)"
    MODE=list
  elif [[ -n "$FETCH_DATE" ]]; then
    [[ "$FETCH_DATE" =~ $DATE_PATTERN ]] || die "--fetch: '$(printable "$FETCH_DATE")' is not an India date such as 2026-10-30"
    if [[ -n "$THROUGH_DATE" && ! "$THROUGH_DATE" =~ $DATE_PATTERN ]]; then
      die "--through: '$(printable "$THROUGH_DATE")' is not an India date such as 2026-10-30"
    fi
    [[ -n "$KEY_FILE" ]] || die "--key <file> is required: the private key that opens the copies (see --help)"
    MODE=fetch
  elif [[ -n "$THROUGH_DATE$KEY_FILE$OUTPUT" ]]; then
    die "--through, --key and --output belong to --fetch <first yyyy-mm-dd> (see --help)"
  fi
}

india_day_of() {
  printf '%s' "$(( ($1 + INDIA_OFFSET_SECONDS) / DAY_SECONDS ))"
}

# An India day's number counts the days from 1970-01-01 in India time, so the UTC date that many days on is its date.
describe_day() {
  local names
  names="$(utc_time "$(( $1 * DAY_SECONDS ))" '%Y-%m-%d %Y/%m/%Y%m%d.json.gz.p7m')" || return 1
  DAY_DATE="${names% *}"
  DAY_NAME="${names#* }"
  DAY_START=$(( $1 * DAY_SECONDS - INDIA_OFFSET_SECONDS ))
  DAY_END=$(( DAY_START + DAY_SECONDS ))
}

# journalctl shows the entries at its --until too, so a day ends in the microsecond before the next one begins.
day_bounds() {
  DAY_SINCE="$(utc_time "$DAY_START" '%Y-%m-%d %H:%M:%S UTC')" || return 1
  DAY_UNTIL="$(utc_time "$(( DAY_END - 1 ))" '%Y-%m-%d %H:%M:%S.999999 UTC')" || return 1
}

line_count() {
  local count
  count="$(wc -l < "$1")" || return 1
  printf '%s' "${count//[[:space:]]/}"
}

entries_outside_day() {
  awk -v first="$(( DAY_START * 1000000 ))" -v after="$(( DAY_END * 1000000 ))" '
    !match($0, /"__REALTIME_TIMESTAMP"[ \t]*:[ \t]*"[0-9]+"/) { outside++; next }
    {
      stamp = substr($0, RSTART, RLENGTH)
      sub(/^"__REALTIME_TIMESTAMP"[ \t]*:[ \t]*"/, "", stamp)
      sub(/"$/, "", stamp)
      if (stamp + 0 < first + 0 || stamp + 0 >= after + 0) outside++
    }
    END { print outside + 0 }' "$1"
}

use_journal_container() {
  read_backup_account "$ENV_FILE" || return 1
  require_backup_client_certificate || return 1
  BACKUP_CONTAINER_NAME="$JOURNAL_CONTAINER"
}

pull_azure_cli() {
  backup_compose pull --policy missing azure-cli < /dev/null
}

read_copies() {
  local listing name created since
  listing="$(backup_blob list --num-results '*' --query '[].[name, properties.creationTime]' --output tsv)" \
    || fail "cannot list the copies of $BACKUP_CONTAINER_NAME (see the Azure CLI's error above)"
  while IFS=$'\t' read -r name created; do
    created="${created%$'\r'}"
    if [[ -z "$name" ]]; then
      continue
    fi
    if [[ ! "$name" =~ $JOURNAL_COPY_PATTERN ]]; then
      OTHERS=$(( OTHERS + 1 ))
      continue
    fi
    [[ -z "${CREATED_OF[$name]:-}" ]] || fail "$(listed_twice "$name")"
    since="$(creation_epoch "$created")" \
      || fail "the copy $(printable "$name") reports the creation time '$(printable "$created")', which is not a time"
    CREATED_OF[$name]="$since"
    CREATED_TEXT[$name]="$created"
  done <<< "$listing"
}

read_settings() {
  STEP='settings'
  use_journal_container || stop
  check_recipient_certificate "$BACKUP_RECIPIENT_FILE" || stop
  log_info "settings: the container $BACKUP_CONTAINER_NAME of $BACKUP_ENDPOINT as the service principal $BACKUP_CLIENT_ID, sealed to the certificate with $RECIPIENT_SUMMARY"
}

check_journal_access() {
  STEP='journal'
  local groups
  groups="$(id -Gn)" || fail "cannot read the groups this run holds"
  if [[ " $groups " != *" $JOURNAL_GROUP "* ]]; then
    fail "this run lacks the group $JOURNAL_GROUP, so journalctl would show it its own entries alone; erp-ai-pro-journal.service gives it the group (sudo systemctl start erp-ai-pro-journal.service)"
  fi
  log_info "journal: this run reads the whole journal with the group $JOURNAL_GROUP"
}

pull_images() {
  STEP='images'
  pull_azure_cli || fail "cannot pull the Azure CLI image of $BACKUP_COMPOSE_FILE (see the error above)"
  log_info "images: the Azure CLI image of $BACKUP_COMPOSE_FILE is on the server"
}

survey_copies() {
  STEP='list'
  local first="$1" last="$2" first_date
  read_copies
  describe_day "$first" || fail "cannot name the India day $first"
  first_date="$DAY_DATE"
  describe_day "$last" || fail "cannot name the India day $last"
  log_info "list: $BACKUP_CONTAINER_NAME holds ${#CREATED_OF[@]} copy(ies) and $OTHERS other blob(s); the days from $first_date to $DAY_DATE it lacks are copied"
}

open_stage() {
  STEP='stage'
  STAGE="$(private_stage journal)" || stop
}

copy_day() {
  local packed="$STAGE/day.json.gz" entries size
  describe_day "$1" || fail "cannot name the India day $1"
  if [[ -n "${CREATED_OF[$DAY_NAME]:-}" ]]; then
    if (( CREATED_OF[$DAY_NAME] + COPY_TIME_TOLERANCE_SECONDS < DAY_END )); then
      PREMADE+=("$DAY_NAME")
      log_warn "day $DAY_DATE: the storage service created $DAY_NAME at $(printable "${CREATED_TEXT[$DAY_NAME]}"), before the day ended, so it cannot hold the day"
    else
      ALREADY=$(( ALREADY + 1 ))
    fi
    return 0
  fi
  STEP="day $DAY_DATE"
  day_bounds || fail "cannot write the bounds of $DAY_DATE"
  # The runtime folder is a tmpfs in memory, so gzip takes the day as journalctl writes it and only its compressed form is kept.
  journalctl --output=json --all --no-pager --quiet --since "$DAY_SINCE" --until "$DAY_UNTIL" | gzip -n -c > "$packed" \
    || fail "cannot export the journal of $DAY_DATE (see the error of journalctl or gzip above)"
  entries="$(gzip -d -c -- "$packed" | wc -l)" || fail "cannot count the entries of $DAY_DATE"
  entries="${entries//[[:space:]]/}"
  if (( entries == 0 )); then
    rm -f -- "$packed"
    EMPTY=$(( EMPTY + 1 ))
    log_info "day $DAY_DATE: the journal holds no entry of it, so there is nothing to copy"
    return 0
  fi
  BACKUP_ARCHIVE="$STAGE/day.json.gz.p7m"
  seal_file "$packed" "$BACKUP_ARCHIVE" || fail "OpenSSL cannot seal the journal of $DAY_DATE (see its error above)"
  rm -f -- "$packed"
  # The Azure CLI container runs as 1654 and reads the sealed copy alone through a read-only mount; the folder stays private.
  chmod 644 -- "$BACKUP_ARCHIVE" || fail "cannot let the Azure CLI container read $BACKUP_ARCHIVE"
  size="$(file_size "$BACKUP_ARCHIVE")" || fail "cannot read the size of $BACKUP_ARCHIVE"
  log_info "day $DAY_DATE ($DAY_SINCE to $DAY_UNTIL): $entries entries, $size bytes sealed for the certificate with serial $RECIPIENT_SERIAL"
  STEP="upload $DAY_DATE"
  backup_blob upload --name "$DAY_NAME" --file "$BACKUP_UPLOAD_PATH" --overwrite false --validate-content --no-progress \
    --output none || fail "cannot upload $DAY_NAME into $BACKUP_CONTAINER_NAME (see the Azure CLI's error above)"
  UPLOADED+=("$DAY_DATE")
  STEP="check $DAY_DATE"
  check_copy
  rm -f -- "$BACKUP_ARCHIVE"
}

check_copy() {
  local length md5 expected_length expected_md5
  blob_properties "$DAY_NAME" '[properties.contentLength, properties.contentSettings.contentMd5]' \
    || fail "cannot read $BACKUP_CONTAINER_NAME/$DAY_NAME back (see the Azure CLI's error above)"
  length="${BLOB_PROPERTIES[0]:-}" md5="${BLOB_PROPERTIES[1]:-}"
  expected_length="$(file_size "$BACKUP_ARCHIVE")" || fail "cannot read the size of $BACKUP_ARCHIVE"
  expected_md5="$(file_md5_base64 "$BACKUP_ARCHIVE")" || fail "cannot hash $BACKUP_ARCHIVE"
  if [[ "$length" != "$expected_length" || "$md5" != "$expected_md5" ]]; then
    fail "$BACKUP_CONTAINER_NAME/$DAY_NAME reads back as $(printable "${length:-nothing}") bytes with the MD5 $(printable "${md5:-nothing}"), not the $expected_length bytes with the MD5 $expected_md5 that were sealed; the write-once policy keeps it, and later runs count its day as copied, so open it with journal.sh --fetch $DAY_DATE --key <file>"
  fi
  log_info "check: $BACKUP_CONTAINER_NAME/$DAY_NAME holds the $length bytes that were sealed, MD5 $md5"
}

prune_copies() {
  STEP='prune'
  local name cutoff kept
  local -a names=() expired=()
  cutoff=$(( NOW - PRUNE_AGE_SECONDS ))
  if (( ${#CREATED_OF[@]} > 0 )); then
    mapfile -t names < <(printf '%s\n' "${!CREATED_OF[@]}" | sort)
  fi
  for name in "${names[@]}"; do
    if (( CREATED_OF[$name] < cutoff )); then
      expired+=("$name")
    fi
  done
  for name in "${expired[@]}"; do
    backup_blob delete --name "$name" --output none \
      || fail "cannot delete $name, older than $JOURNAL_IMMUTABILITY_DAYS days (see the Azure CLI's error above)"
    DELETED=$(( DELETED + 1 ))
    log_info "prune: deleted $name, created $(printable "${CREATED_TEXT[$name]}")"
  done
  kept=$(( ${#names[@]} - DELETED + ${#UPLOADED[@]} ))
  log_info "prune: deleted $DELETED copy(ies) older than $JOURNAL_IMMUTABILITY_DAYS days, kept $kept, left $OTHERS other blob(s) alone"
}

refuse_premade() {
  if (( ${#PREMADE[@]} == 0 )); then
    return 0
  fi
  STEP='copies'
  fail "the storage service created ${PREMADE[*]} before their day ended, so they cannot hold it, and the write-once policy keeps them $JOURNAL_IMMUTABILITY_DAYS days: whoever holds the backup service principal's certificate made them, and the journal of those days is on this server alone (docs/operations/runbooks/host-setup.md)"
}

copy_journal() {
  local today day copied='none'
  NOW="$(date -u +%s)"
  [[ "$NOW" =~ ^[0-9]{10,}$ ]] || fail "cannot read this server's clock"
  check_journal_access
  read_settings
  pull_images
  today="$(india_day_of "$NOW")"
  survey_copies "$(( today - COPY_WINDOW_DAYS ))" "$(( today - 1 ))"
  open_stage
  for (( day = today - COPY_WINDOW_DAYS; day < today; day++ )); do
    copy_day "$day"
  done
  prune_copies
  refuse_premade
  if (( ${#UPLOADED[@]} > 0 )); then
    copied="${UPLOADED[*]}"
  fi
  printf 'copied the journal of %s day(s) to %s (%s); %s day(s) had a copy already and %s held no entry; %s copy(ies) older than %s days deleted\n' \
    "${#UPLOADED[@]}" "$BACKUP_CONTAINER_NAME" "$copied" "$ALREADY" "$EMPTY" "$DELETED" "$JOURNAL_IMMUTABILITY_DAYS"
}

copy_day_end() {
  local stamp="${1##*/}" midnight
  midnight="$(date -u -d "${stamp:0:4}-${stamp:4:2}-${stamp:6:2}" +%s 2> /dev/null)" || return 1
  printf '%s' "$(( midnight + DAY_SECONDS - INDIA_OFFSET_SECONDS ))"
}

list_journal() {
  local name end mark
  local -a rows=()
  use_journal_container || die "nothing was listed"
  pull_azure_cli || die "cannot pull the Azure CLI image of $BACKUP_COMPOSE_FILE; nothing was listed"
  read_copies
  for name in "${!CREATED_OF[@]}"; do
    mark=''
    if ! end="$(copy_day_end "$name")"; then
      mark='   its name names no day of the calendar'
    elif (( CREATED_OF[$name] + COPY_TIME_TOLERANCE_SECONDS < end )); then
      mark='   created before its day ended, so it cannot hold that day'
    fi
    rows+=("${CREATED_OF[$name]}"$'\t'"$name   created $(printable "${CREATED_TEXT[$name]}")$mark")
  done
  if (( ${#rows[@]} > 0 )); then
    printf '%s\n' "${rows[@]}" | sort -t $'\t' -k1,1nr | cut -f2-
  fi
  printf '%s copy(ies) in %s, the newest creation first; %s other blob(s) left out\n' "${#rows[@]}" "$BACKUP_CONTAINER_NAME" "$OTHERS"
}

india_day_of_date() {
  local midnight
  midnight="$(date -u -d "$1" +%s 2> /dev/null)" || return 1
  [[ "$(utc_time "$midnight" '%Y-%m-%d')" == "$1" ]] || return 1
  printf '%s' "$(( midnight / DAY_SECONDS ))"
}

report_fetch_failure() {
  if (( ${#WRITTEN[@]} > 0 )); then
    log_error "written before the failure, into $OUTPUT: ${WRITTEN[*]}; once its cause is fixed, fetch the days after them into another folder"
    return 0
  fi
  if (( OUTPUT_CREATED )); then
    rmdir -- "$OUTPUT" 2> /dev/null || true
  fi
  log_error "no day was written"
}

open_output_folder() {
  if (( OUTPUT_CREATED )); then
    return 0
  fi
  mkdir -m 700 -- "$OUTPUT" || die "cannot create the folder $OUTPUT"
  OUTPUT_CREATED=1
}

fetch_day() {
  local sealed="$STAGE/day.json.gz.p7m" packed="$STAGE/day.json.gz" plain="$STAGE/day.json" length md5 created since entries outside target
  describe_day "$1" || die "cannot name the India day $1"
  day_bounds || die "cannot write the bounds of $DAY_DATE"
  blob_properties "$DAY_NAME" '[properties.contentLength, properties.contentSettings.contentMd5, properties.creationTime]' \
    || die "cannot read $DAY_NAME in $BACKUP_CONTAINER_NAME (see the Azure CLI's error above); journal.sh --list names the copies it holds, and a day of which the journal held no entry has none"
  length="${BLOB_PROPERTIES[0]:-}" md5="${BLOB_PROPERTIES[1]:-}" created="${BLOB_PROPERTIES[2]:-}"
  since="$(creation_epoch "$created")" \
    || die "the copy $DAY_NAME reports the creation time '$(printable "$created")', which is not a time"
  if (( since + COPY_TIME_TOLERANCE_SECONDS < DAY_END )); then
    die "the storage service created $DAY_NAME at $(printable "$created"), before its day ended at $DAY_UNTIL, so it cannot hold that day: whoever holds the backup service principal's certificate made it, and the journal of the day is on this server alone"
  fi
  backup_blob download --name "$DAY_NAME" --no-progress --output none > "$sealed" \
    || die "cannot download $DAY_NAME (see the Azure CLI's error above)"
  if [[ "$(file_size "$sealed")" != "$length" || "$(file_md5_base64 "$sealed")" != "$md5" ]]; then
    die "the download of $DAY_NAME does not match its length $(printable "$length") and MD5 $(printable "$md5") in the container"
  fi
  if ! open_sealed "$sealed" "$packed"; then
    rm -f -- "$packed"
    die "the key does not open $DAY_NAME, or the copy was changed: $(key_mismatch_hint "$sealed" 'the copy')"
  fi
  rm -f -- "$sealed"
  gzip -d -c -- "$packed" > "$plain" || die "the opened copy of $DAY_DATE is not gzip data"
  rm -f -- "$packed"
  entries="$(line_count "$plain")" || die "cannot count the entries of $DAY_DATE"
  outside="$(entries_outside_day "$plain")" || die "cannot read the entries of $DAY_DATE"
  if (( outside > 0 )); then
    die "$outside of the $entries entries of $DAY_NAME lie outside the India day $DAY_DATE ($DAY_SINCE to $DAY_UNTIL), so it is no copy of that day"
  fi
  open_output_folder
  target="$OUTPUT/journal-$DAY_DATE.json"
  install_file_atomically "$plain" "$target" || die "cannot write $target"
  rm -f -- "$plain"
  WRITTEN+=("$DAY_DATE")
  log_info "day $DAY_DATE ($DAY_SINCE to $DAY_UNTIL): $entries entries from $DAY_NAME, $length bytes with the MD5 $md5, created by the storage service at $(printable "$created"), written to $target"
}

fetch_days() {
  local first last day count
  check_key
  NOW="$(date -u +%s)"
  [[ "$NOW" =~ ^[0-9]{10,}$ ]] || die "cannot read this server's clock"
  first="$(india_day_of_date "$FETCH_DATE")" || die "--fetch: '$FETCH_DATE' is not a day of the calendar"
  THROUGH_DATE="${THROUGH_DATE:-$FETCH_DATE}"
  last="$(india_day_of_date "$THROUGH_DATE")" || die "--through: '$THROUGH_DATE' is not a day of the calendar"
  (( last >= first )) || die "--through: $THROUGH_DATE comes before $FETCH_DATE, the first day"
  count=$(( last - first + 1 ))
  (( count <= FETCH_MAX_DAYS )) \
    || die "the period from $FETCH_DATE to $THROUGH_DATE holds $count days, more than the $FETCH_MAX_DAYS one fetch takes; fetch it in parts"
  describe_day "$last" || die "cannot name the India day $THROUGH_DATE"
  day_bounds || die "cannot write the bounds of $THROUGH_DATE"
  if (( DAY_END > NOW )); then
    die "the India day $THROUGH_DATE lasts until $DAY_UNTIL, so it has no copy yet: a day is copied the night after it ends"
  fi
  OUTPUT="${OUTPUT:-$HOST_ROOT$DEPLOY_HOME/journal-$FETCH_DATE-to-$THROUGH_DATE}"
  if [[ -e "$OUTPUT" || -L "$OUTPUT" ]]; then
    die "--output: $(printable "$OUTPUT") exists; move it away or name another folder"
  fi
  [[ -d "$(dirname -- "$OUTPUT")" ]] || die "--output: the folder that would hold $(printable "$OUTPUT") does not exist"
  use_journal_container || exit 1
  pull_azure_cli || die "cannot pull the Azure CLI image of $BACKUP_COMPOSE_FILE"
  STAGE="$(private_stage journal)" || exit 1
  for (( day = first; day <= last; day++ )); do
    fetch_day "$day"
  done
  shred_key
  printf 'wrote the journal of %s day(s), %s to %s, from %s into %s (0700), one 0600 file journal-<yyyy-mm-dd>.json a day; copy the folder off the server, then shred it on the server (shred -u %s/*.json && rmdir %s)\n' \
    "$count" "$FETCH_DATE" "$THROUGH_DATE" "$BACKUP_CONTAINER_NAME" "$OUTPUT" "$OUTPUT" "$OUTPUT"
}

main() {
  parse_journal_args "$@"
  require_command docker openssl gzip date mktemp base64 sort cut awk wc
  if [[ "$MODE" == copy ]]; then
    require_command journalctl
  elif [[ "$MODE" == fetch ]]; then
    require_command shred
  fi
  require_release_host
  umask 077
  trap finish EXIT
  trap 'exit 129' HUP
  trap 'exit 130' INT
  trap 'exit 143' TERM
  case "$MODE" in
    copy) copy_journal ;;
    list) list_journal ;;
    fetch) fetch_days ;;
  esac
}

main "$@"
