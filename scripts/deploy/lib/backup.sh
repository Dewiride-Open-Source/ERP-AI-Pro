#!/usr/bin/env bash

if [[ -n "${ERP_DEPLOY_BACKUP_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_DEPLOY_BACKUP_SH_LOADED=1

# shellcheck source=lib/release.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/release.sh"

readonly BACKUP_COMPOSE_FILE='compose.backup.yaml'
readonly BACKUP_CLIENT_CERTIFICATE='erp-backup-client.pem'
readonly BACKUP_RECIPIENT_CERTIFICATE='erp-backup-recipient.crt'
readonly BACKUP_CLIENT_CERTIFICATE_FILE="$COMPOSE_DIR/secrets/$BACKUP_CLIENT_CERTIFICATE"
readonly BACKUP_RECIPIENT_FILE="$COMPOSE_DIR/secrets/$BACKUP_RECIPIENT_CERTIFICATE"
readonly BACKUP_VOLUMES=(erp-ai-pro-data-protection-keys erp-ai-pro-proxy-data)
readonly BACKUP_VOLUME_SERVICES=(key-ring proxy-data)
readonly BACKUP_VOLUME_OWNERS=(1654:1654 10001:10001)
readonly BACKUP_VOLUME_PARTS=(data-protection-keys.tar proxy-data.tar)
readonly BACKUP_VOLUME_MODE='700'
readonly BACKUP_MANIFEST='manifest'
readonly BACKUP_MANIFEST_FORMAT='ERP-AI-Pro server backup 1'
readonly BACKUP_ENV_PART='.env'
readonly BACKUP_PREVIOUS_ENV_PART='.env.previous'
readonly BACKUP_ARCHIVE_PATTERN='^[0-9]{4}/(0[1-9]|1[0-2])/[0-9]{8}T[0-9]{6}Z\.tar\.p7m$'
readonly BACKUP_CREATION_PATTERN='^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,9})?(Z|\+00:00)$'
readonly BACKUP_IMMUTABILITY_DAYS=90
readonly JOURNAL_CONTAINER='host-journal'
readonly JOURNAL_IMMUTABILITY_DAYS=365
readonly JOURNAL_COPY_PATTERN='^[0-9]{4}/(0[1-9]|1[0-2])/[0-9]{8}\.json\.gz\.p7m$'
readonly BACKUP_MINIMUM_KEY_BITS=4096
readonly BACKUP_CERTIFICATE_WARNING_SECONDS=2592000
readonly BACKUP_UPLOAD_PATH='/backup/archive.tar.p7m'
readonly BACKUP_STACK_PROJECT_LABEL='com.docker.compose.project=erp-ai-pro'
readonly BACKUP_PROXY_ROOT_MEMBER='./caddy/pki/authorities/local/root.crt'
readonly BACKUP_KEY_MEMBER_PATTERN='^\./key-([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\.xml$'
readonly GUID_PATTERN='^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
readonly BLOB_ENDPOINT_PATTERN='^https://[a-z0-9]{3,24}\.blob\.core\.windows\.net/?$'
readonly CONTAINER_NAME_PATTERN='^[a-z0-9](-?[a-z0-9])+$'
# CMS seals each archive's random AES-256-GCM key with RSA-OAEP, SHA-256 and MGF1-SHA-256 (RFC 8017, section 7.1); without these
# options OpenSSL would use PKCS #1 v1.5.
readonly CMS_RECIPIENT_OPTIONS=(-keyopt rsa_padding_mode:oaep -keyopt rsa_oaep_md:sha256 -keyopt rsa_mgf1_md:sha256)

BACKUP_ENDPOINT=''
BACKUP_CONTAINER_NAME=''
BACKUP_CLIENT_ID=''
BACKUP_TENANT_ID=''
BACKUP_ARCHIVE=''
BLOB_PROPERTIES=()
RECIPIENT_SERIAL=''
RECIPIENT_SUMMARY=''
KEY_FILE=''
KEY_CHECKED=0
KEY_SHREDDED=0

backup_compose() {
  (cd -- "$COMPOSE_DIR" && host_docker compose --progress quiet -f "$BACKUP_COMPOSE_FILE" "$@")
}

# Standard input reaches the container, where the writer's tar reads an archive, so a caller that sends none gives it /dev/null.
backup_volume() {
  local index="$1" role="$2"
  shift 2
  backup_compose run --rm -T --quiet-pull "${BACKUP_VOLUME_SERVICES[$index]}-$role" "$@"
}

# An upload reads the sealed archive that BACKUP_ARCHIVE names, mounted read-only: of the private stage, the container sees
# that ciphertext alone.
backup_blob() {
  local action="$1"
  shift
  local -a mount=()
  if [[ "$action" == upload ]]; then
    mount=(-v "$BACKUP_ARCHIVE:$BACKUP_UPLOAD_PATH:ro")
  fi
  backup_compose run --rm -T --quiet-pull -e "BACKUP_CLIENT_ID=$BACKUP_CLIENT_ID" -e "BACKUP_TENANT_ID=$BACKUP_TENANT_ID" \
    "${mount[@]}" azure-cli storage blob "$action" --auth-mode login --blob-endpoint "$BACKUP_ENDPOINT" \
    --container-name "$BACKUP_CONTAINER_NAME" "$@" < /dev/null
}

# The Azure CLI's tsv output writes each value of a flat multiselect list on a line of its own.
blob_properties() {
  local answer
  answer="$(backup_blob show --name "$1" --query "$2" --output tsv)" || return 1
  mapfile -t BLOB_PROPERTIES <<< "${answer//$'\r'/}"
}

seal_file() {
  openssl cms -encrypt -binary -aes-256-gcm -in "$1" -outform DER -out "$2" -recip "$BACKUP_RECIPIENT_FILE" "${CMS_RECIPIENT_OPTIONS[@]}"
}

open_sealed() {
  openssl cms -decrypt -binary -inform DER -in "$1" -inkey "$KEY_FILE" -out "$2"
}

check_key() {
  if [[ ! -f "$KEY_FILE" || -L "$KEY_FILE" || ! -r "$KEY_FILE" ]]; then
    die "--key: '$(printable "$KEY_FILE")' is not a regular file this account can read; nothing was changed"
  fi
  openssl pkey -in "$KEY_FILE" -noout 2> /dev/null \
    || die "--key: $KEY_FILE holds no private key that OpenSSL reads without a password, as export-client-certificate.sh backup-recipient-key writes it; nothing was changed"
  KEY_CHECKED=1
}

key_mismatch_hint() {
  local sealed="$1" what="${2:-the archive}" parsed='' line depth inside=0 serial key_serial=''
  local -a recipients=()
  parsed="$(openssl asn1parse -inform DER -in "$sealed" 2> /dev/null)" || parsed=''
  # Inside the recipientInfos SET (depth 3), each IssuerAndSerialNumber's serial is the INTEGER at depth 6. asn1parse prints
  # every INTEGER in hexadecimal, where cms -print shows one below 128 bits in decimal.
  while IFS= read -r line; do
    [[ "$line" =~ :d=([0-9]+) ]] || continue
    depth="${BASH_REMATCH[1]}"
    if (( depth == 3 )); then
      inside=0
      if [[ "$line" == *'cons: SET'* ]]; then
        inside=1
      fi
    elif (( inside && depth == 6 )) && [[ "$line" == *'prim: INTEGER'* ]]; then
      serial="${line##*:}"
      serial="${serial#"${serial%%[!0]*}"}"
      recipients+=("${serial:-0}")
    fi
  done <<< "$parsed"
  key_serial="$(openssl x509 -in "$KEY_FILE" -noout -serial 2> /dev/null | sed -n 's/^serial=0*//p')" || key_serial=''
  printf '%s is sealed to the certificate with the serial number %s, and the key file holds %s; export the version with that serial number (export-client-certificate.sh backup-recipient-key --version <version>)' \
    "$what" "$(printable "${recipients[*]:-unreadable}")" "$(printable "${key_serial:-no certificate}")"
}

shred_key() {
  if shred -u -- "$KEY_FILE"; then
    KEY_SHREDDED=1
    log_info "shredded the key file $KEY_FILE"
  else
    log_warn "cannot shred $KEY_FILE (see the error above)"
  fi
}

warn_key_left() {
  if (( KEY_CHECKED && ! KEY_SHREDDED )) && [[ -e "$KEY_FILE" ]]; then
    log_warn "the key stays at $KEY_FILE; shred it once you are done (shred -u $KEY_FILE), because it never belongs on the server"
  fi
}

# Prints the value of the one KEY=value line of an env file, read as data and never sourced: compose reads .env with its own
# rules, so a key set in another form or twice could mean one thing here and another there.
env_setting() {
  local file="$1" key="$2" line number=0 value='' found=0
  local any_form="^[[:space:]]*(export[[:space:]]+)?$key[[:space:]]*[=:]" canonical="^$key=(.*)$"
  while IFS= read -r line || [[ -n "$line" ]]; do
    number=$(( number + 1 ))
    [[ "$line" =~ $any_form ]] || continue
    if [[ ! "$line" =~ $canonical ]]; then
      log_error "line $number of $file sets $key in another form than $key=<value>"
      return 1
    fi
    if (( found )); then
      log_error "$file sets $key twice"
      return 1
    fi
    value="${BASH_REMATCH[1]}"
    found=1
  done < "$file"
  printf '%s' "$value"
}

read_backup_settings() {
  local file="$1" endpoint container client tenant
  if [[ ! -f "$file" ]]; then
    log_error "$file is missing; bootstrap.sh writes it from .env.example"
    return 1
  fi
  endpoint="$(env_setting "$file" BACKUP_BLOB_ENDPOINT)" || return 1
  container="$(env_setting "$file" BACKUP_CONTAINER)" || return 1
  client="$(env_setting "$file" BACKUP_AZURE_CLIENT_ID)" || return 1
  tenant="$(env_setting "$file" AZURE_TENANT_ID)" || return 1
  if [[ ! "$endpoint" =~ $BLOB_ENDPOINT_PATTERN ]]; then
    log_error "BACKUP_BLOB_ENDPOINT in $file is '$(printable "$endpoint")', not the backup account's blob endpoint https://<account>.blob.core.windows.net/ that provision.sh prints"
    return 1
  fi
  if (( ${#container} < 3 || ${#container} > 63 )) || [[ ! "$container" =~ $CONTAINER_NAME_PATTERN ]]; then
    log_error "BACKUP_CONTAINER in $file is '$(printable "$container")', not a container name of 3 to 63 lowercase letters, digits and single hyphens"
    return 1
  fi
  if [[ ! "$client" =~ $GUID_PATTERN ]]; then
    log_error "BACKUP_AZURE_CLIENT_ID in $file is '$(printable "$client")', not the backup service principal's application id that entra.sh prints"
    return 1
  fi
  if [[ ! "$tenant" =~ $GUID_PATTERN ]]; then
    log_error "AZURE_TENANT_ID in $file is '$(printable "$tenant")', not the Entra tenant id"
    return 1
  fi
  BACKUP_ENDPOINT="${endpoint%/}"
  BACKUP_CONTAINER_NAME="$container"
  BACKUP_CLIENT_ID="$client"
  BACKUP_TENANT_ID="$tenant"
}

require_backup_client_certificate() {
  if [[ ! -f "$BACKUP_CLIENT_CERTIFICATE_FILE" ]]; then
    log_error "$BACKUP_CLIENT_CERTIFICATE_FILE is missing: the backup service principal's certificate, which export-client-certificate.sh backup exports (docs/operations/runbooks/host-setup.md)"
    return 1
  fi
}

# The server holds the recipient's public certificate alone, so it can seal an archive but never open one, an old one included.
check_recipient_certificate() {
  local file="$1" text identity serial fingerprint expires
  if [[ ! -f "$file" || ! -r "$file" ]]; then
    log_error "cannot read $file, the public certificate the archives are sealed to, which bootstrap.sh --backup-recipient places (docs/operations/runbooks/host-setup.md)"
    return 1
  fi
  if grep -q -- 'PRIVATE KEY' "$file"; then
    log_error "$file holds a private key, which never belongs on the server: destroy it (shred -u $file) and place the public certificate alone, from export-client-certificate.sh backup-recipient"
    return 1
  fi
  if ! text="$(openssl x509 -in "$file" -noout -text 2> /dev/null)"; then
    log_error "$file is not a PEM certificate"
    return 1
  fi
  local algorithm='Public Key Algorithm: rsaEncryption' size='Public-Key: \(([0-9]+) bit\)'
  if [[ "$text" != *"$algorithm"* || ! "$text" =~ $size ]] || (( BASH_REMATCH[1] < BACKUP_MINIMUM_KEY_BITS )); then
    log_error "$file does not hold an RSA key of $BACKUP_MINIMUM_KEY_BITS bits or more, the key the archives are sealed to"
    return 1
  fi
  identity="$(openssl x509 -in "$file" -noout -serial -fingerprint -sha256 -enddate)" || return 1
  serial="$(sed -n 's/^serial=//p' <<< "$identity")"
  fingerprint="$(sed -n 's/^[Ss][Hh][Aa]256 Fingerprint=//p' <<< "$identity")"
  expires="$(sed -n 's/^notAfter=//p' <<< "$identity")"
  RECIPIENT_SERIAL="$(printable "$serial")"
  RECIPIENT_SUMMARY="serial $RECIPIENT_SERIAL, SHA-256 fingerprint $(printable "$fingerprint"), valid until $(printable "$expires")"
  if ! openssl x509 -in "$file" -noout -checkend "$BACKUP_CERTIFICATE_WARNING_SECONDS" > /dev/null; then
    log_warn "the certificate the archives are sealed to ($RECIPIENT_SUMMARY) expires within 30 days or has expired; the archives still open with its key, and secrets.md says how to place the next one"
  fi
}

# Plaintext stays on a file system kept in memory: the unit's RuntimeDirectory under /run, or /dev/shm for a run by hand.
private_stage() {
  local parent="${RUNTIME_DIRECTORY:-$HOST_ROOT/dev/shm}" stage
  if [[ ! -d "$parent" ]]; then
    log_error "$parent, where the plaintext copy is kept in memory, does not exist"
    return 1
  fi
  if ! stage="$(mktemp -d "$parent/$1.XXXXXXXX")"; then
    log_error "cannot create a private folder in $parent"
    return 1
  fi
  printf '%s' "$stage"
}

file_sha256() {
  local output
  output="$(sha256sum -- "$1")" || return 1
  printf '%s' "${output%% *}"
}

file_size() {
  local size
  size="$(wc -c < "$1")" || return 1
  printf '%s' "${size//[[:space:]]/}"
}

file_md5_base64() {
  local digest
  digest="$(openssl dgst -md5 -binary "$1" | base64)" || return 1
  printf '%s' "$digest"
}

utc_time() {
  date -u -d "@$1" "+$2"
}

archive_name_of() {
  printf '%s.tar.p7m' "$(utc_time "$1" '%Y/%m/%Y%m%dT%H%M%SZ')"
}

creation_epoch() {
  # The Azure CLI writes a blob's name into its tab-separated listing as it is, so a name that holds a tab or a line break
  # can forge the fields that follow; a creation time of any other shape than the service's is refused, never read by date.
  [[ "$1" =~ $BACKUP_CREATION_PATTERN ]] || return 1
  date -u -d "$1" +%s 2> /dev/null
}

listed_twice() {
  printf 'the listing of %s names %s twice, which the storage service never does: a blob whose name holds a line break forges listing rows' \
    "$BACKUP_CONTAINER_NAME" "$(printable "$1")"
}

named_epoch() {
  local stamp="${1##*/}"
  date -u -d "${stamp:0:4}-${stamp:4:2}-${stamp:6:2}T${stamp:9:2}:${stamp:11:2}:${stamp:13:2}Z" +%s 2> /dev/null
}

tar_members() {
  tar -tf "$1"
}

entry_count() {
  local members="$1" member count=0
  while IFS= read -r member; do
    if [[ -n "$member" && "$member" != ./ && "$member" != . ]]; then
      count=$(( count + 1 ))
    fi
  done <<< "$members"
  printf '%s' "$count"
}

proxy_root_fingerprint() {
  local part="$1" members certificate output
  members="$(tar_members "$part")" || return 1
  if ! grep -qxF -- "$BACKUP_PROXY_ROOT_MEMBER" <<< "$members"; then
    printf 'none'
    return 0
  fi
  certificate="$(tar -xOf "$part" "$BACKUP_PROXY_ROOT_MEMBER")" || return 1
  output="$(openssl x509 -noout -fingerprint -sha256 <<< "$certificate")" || return 1
  printf '%s' "$(printable "${output#*=}")"
}

# One line per key of the key ring, with the Key Vault key version that wraps it: secrets.md keeps every such version enabled,
# or the restored key ring cannot be read.
key_ring_lines() {
  local part="$1" members member text id expires kid
  local expiration_pattern='<expirationDate>([0-9T:.+Z-]{1,40})</expirationDate>'
  local kid_pattern='<kid>(https://[A-Za-z0-9.-]{1,100}/keys/[A-Za-z0-9-]{1,127}/[0-9a-f]{32})</kid>'
  members="$(tar_members "$part")" || return 1
  while IFS= read -r member; do
    [[ "$member" =~ $BACKUP_KEY_MEMBER_PATTERN ]] || continue
    id="${BASH_REMATCH[1]}"
    text="$(tar -xOf "$part" "$member")" || return 1
    expires=none
    kid=none
    if [[ "$text" =~ $expiration_pattern ]]; then
      expires="${BASH_REMATCH[1]}"
    fi
    if [[ "$text" =~ $kid_pattern ]]; then
      kid="${BASH_REMATCH[1]}"
    fi
    printf 'data-protection-key %s expires %s wrapped-by %s\n' "$id" "$expires" "$kid"
  done <<< "$members"
}

# The staged file takes the target's place in one rename, so a crash leaves one whole file or the other.
install_file_atomically() {
  local source="$1" target="$2" staged
  if ! staged="$(mktemp "$target.staged.XXXXXX")"; then
    log_error "cannot create a file next to $target"
    return 1
  fi
  if cp -- "$source" "$staged" && chmod 600 -- "$staged" && mv -f -- "$staged" "$target"; then
    return 0
  fi
  rm -f -- "$staged"
  log_error "cannot write $target"
  return 1
}
