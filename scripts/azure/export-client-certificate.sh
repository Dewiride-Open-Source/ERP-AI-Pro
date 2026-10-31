#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/export-client-certificate.sh <runtime|migrator|backup> [--out <file>] [--params <file>]
       bash scripts/azure/export-client-certificate.sh backup-recipient [--out <file>] [--params <file>]
       bash scripts/azure/export-client-certificate.sh backup-recipient-key [--version <version>] [--out <file>] [--params <file>]

Downloads a certificate of the operations Key Vault into a local file for a
one-time copy to the server, validates it with openssl and prints the placement
command: the client certificate (PEM with the private key) of the runtime, the
migrator or the backup service principal; the public certificate of the backup
encryption certificate, which the server seals every backup archive to; or, for
a restore only, that certificate with its private key, the only key that opens
the archives. Refuses to overwrite an existing file and never prints the
content.

Options:
  --out <file>          Destination file, relative to the repository root (default: scripts/azure/out/erp-<runtime|migrator|backup>-client.pem,
                        scripts/azure/out/erp-backup-recipient.crt or scripts/azure/out/erp-backup-recipient-key.pem, gitignored).
  --version <version>   backup-recipient-key only: the version of the backup encryption certificate to export, for an archive sealed
                        before a rotation (default: the current version).
  --params <file>       Parameter file (default: scripts/azure/params.env).
  --help                Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/graph.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/graph.sh"

readonly SERVER_CHECKOUT='/srv/erp-ai-pro/ERP-AI-Pro'
readonly SERVER_SECRETS_DIR="$SERVER_CHECKOUT/infra/compose/secrets"
readonly CONTAINER_USER='1654'
readonly EXPORT_TARGETS=("${SERVER_TARGETS[@]}" backup-recipient backup-recipient-key)
readonly RECIPIENT_FILE='erp-backup-recipient.crt'
readonly RECIPIENT_KEY_FILE='erp-backup-recipient-key.pem'

TARGET=''
OUT_FILE=''
VERSION=''

require_export_target() {
  local candidate
  for candidate in "${EXPORT_TARGETS[@]}"; do
    [[ "$1" == "$candidate" ]] && return 0
  done
  die "name the certificate to export: ${EXPORT_TARGETS[*]}; got '$1'"
}

default_out_file() {
  case "$TARGET" in
    backup-recipient) printf '%s/%s' "$ERP_AZURE_TEMP_DIR" "$RECIPIENT_FILE" ;;
    backup-recipient-key) printf '%s/%s' "$ERP_AZURE_TEMP_DIR" "$RECIPIENT_KEY_FILE" ;;
    *) printf '%s/%s' "$ERP_AZURE_TEMP_DIR" "$(client_certificate_file "$TARGET")" ;;
  esac
}

parse_export_args() {
  local -a rest=()
  while (( $# > 0 )); do
    case "$1" in
      --out)
        [[ -n "${2:-}" ]] || die "--out requires a file path"
        OUT_FILE="$2"
        shift
        ;;
      --out=*)
        OUT_FILE="${1#--out=}"
        [[ -n "$OUT_FILE" ]] || die "--out requires a file path"
        ;;
      --version)
        [[ -n "${2:-}" ]] || die "--version requires a certificate version"
        VERSION="$2"
        shift
        ;;
      --version=*)
        VERSION="${1#--version=}"
        [[ -n "$VERSION" ]] || die "--version requires a certificate version"
        ;;
      *)
        rest+=("$1")
        ;;
    esac
    shift
  done
  parse_args "${rest[@]}"
  (( ${#ARGS[@]} == 1 )) || die "name the certificate to export: ${EXPORT_TARGETS[*]}"
  require_export_target "${ARGS[0]}"
  TARGET="${ARGS[0]}"
  if [[ -n "$VERSION" ]]; then
    [[ "$TARGET" == backup-recipient-key ]] || die "--version applies to backup-recipient-key alone; the other certificates are always exported in their current version"
    [[ "$VERSION" =~ ^[0-9a-f]{32}$ ]] || die "--version must be a Key Vault version, 32 lowercase hexadecimal digits; got '$VERSION'"
  fi
  [[ -n "$OUT_FILE" ]] || OUT_FILE="$(default_out_file)"
}

DOWNLOAD_STARTED=0

discard_invalid_file() {
  rm -f -- "$OUT_FILE"
  if [[ "$TARGET" == backup-recipient ]]; then
    die "the downloaded certificate is not a PEM certificate without a private key (see the openssl error above); $OUT_FILE was deleted"
  fi
  die "the downloaded secret is not a PEM certificate with an unencrypted RSA private key (see the openssl error above); $OUT_FILE was deleted"
}

discard_partial_download() {
  if (( DOWNLOAD_STARTED )) && [[ -e "$OUT_FILE" ]]; then
    rm -f -- "$OUT_FILE"
    log_warn "$OUT_FILE was deleted because the export did not complete"
  fi
}

require_ignored_destination() {
  [[ ! -e "$OUT_FILE" ]] || die "$OUT_FILE already exists; delete it or choose another --out path (the file is never overwritten)"
  if [[ "$TARGET" == backup-recipient ]]; then
    [[ "$OUT_FILE" == *.crt ]] || die "--out must end in .crt for the public certificate; got '$OUT_FILE'"
  else
    [[ "$OUT_FILE" == *.pem ]] || die "--out must end in .pem so the file stays covered by .gitignore; got '$OUT_FILE'"
  fi
  if git -C "$REPO_ROOT" rev-parse --is-inside-work-tree > /dev/null 2>&1 && ! git -C "$REPO_ROOT" check-ignore -q -- "$OUT_FILE"; then
    die "--out '$OUT_FILE' is inside the repository but not ignored by git; use the default location or a path under $ERP_AZURE_TEMP_DIR/"
  fi
}

download() {
  local -a version_args=()
  [[ -z "$VERSION" ]] || version_args=(--version "$VERSION")
  case "$TARGET" in
    backup-recipient)
      log_step "Downloading the public certificate of $BACKUP_ENCRYPTION_CERTIFICATE_NAME from $ERP_AZURE_KEYVAULT_OPS_NAME"
      az keyvault certificate download --vault-name "$ERP_AZURE_KEYVAULT_OPS_NAME" --name "$BACKUP_ENCRYPTION_CERTIFICATE_NAME" \
        --encoding PEM --file "$OUT_FILE" --only-show-errors
      ;;
    backup-recipient-key)
      log_step "Downloading $BACKUP_ENCRYPTION_CERTIFICATE_NAME${VERSION:+ version $VERSION} with its private key from $ERP_AZURE_KEYVAULT_OPS_NAME"
      az keyvault secret download --vault-name "$ERP_AZURE_KEYVAULT_OPS_NAME" --name "$BACKUP_ENCRYPTION_CERTIFICATE_NAME" \
        "${version_args[@]}" --file "$OUT_FILE" --only-show-errors
      ;;
    *)
      log_step "Downloading $(client_certificate_name "$TARGET") from $ERP_AZURE_KEYVAULT_OPS_NAME"
      az keyvault secret download --vault-name "$ERP_AZURE_KEYVAULT_OPS_NAME" --name "$(client_certificate_name "$TARGET")" \
        --file "$OUT_FILE" --only-show-errors
      ;;
  esac
}

validate() {
  log_step "Validating with openssl"
  case "$TARGET" in
    backup-recipient)
      openssl x509 -in "$OUT_FILE" -noout -subject -enddate -fingerprint -sha256 || discard_invalid_file
      ! grep -q 'PRIVATE KEY' "$OUT_FILE" || discard_invalid_file
      log_info "the public certificate is valid and holds no private key"
      ;;
    backup-recipient-key)
      openssl x509 -in "$OUT_FILE" -noout -subject -serial -enddate -fingerprint -sha256 || discard_invalid_file
      openssl rsa -in "$OUT_FILE" -check -noout || discard_invalid_file
      local serial
      serial="$(openssl x509 -in "$OUT_FILE" -noout -serial)" || discard_invalid_file
      serial="${serial#serial=}"
      log_info "serial number ${serial#"${serial%%[!0]*}"}, as restore.sh names it"
      log_info "certificate and RSA private key are valid; the serial number names the archives this key opens (their recipient)"
      ;;
    *)
      openssl x509 -in "$OUT_FILE" -noout -subject -enddate || discard_invalid_file
      openssl rsa -in "$OUT_FILE" -check -noout || discard_invalid_file
      log_info "certificate and RSA private key are valid"
      ;;
  esac
}

print_client_certificate_steps() {
  local file_name
  file_name="$(client_certificate_file "$TARGET")"
  log_info "next steps:"
  log_info "  1. copy the file to the server's administrator over an encrypted channel, for example: scp $OUT_FILE <administrator>@<server>:~/$file_name"
  log_info "  2. on the server, as the administrator (the host bootstrap made the folder, docs/operations/runbooks/host-setup.md):"
  log_info "     sudo install -o $CONTAINER_USER -g $CONTAINER_USER -m 0400 ~/$file_name $SERVER_SECRETS_DIR/$file_name"
  log_info "     shred -u ~/$file_name"
  case "$TARGET" in
    backup)
      log_info "     ($CONTAINER_USER is the user the Azure CLI container of the nightly backup runs as, which reads this file alone)"
      ;;
    *)
      log_info "     ($CONTAINER_USER is the uid and gid of the app user in the chiseled aspnet image the api and migrator containers run as)"
      ;;
  esac
  case "$TARGET" in
    migrator)
      log_info "     then set MIGRATOR_AZURE_CLIENT_ID in $SERVER_CHECKOUT/infra/compose/.env to the MIGRATOR_AZURE_CLIENT_ID line entra.sh prints;"
      log_info "     from then on erp-ai-pro-retain.timer runs retain every night, so place it only once the deployment's images are on the server"
      ;;
    backup)
      log_info "     then set BACKUP_AZURE_CLIENT_ID in $SERVER_CHECKOUT/infra/compose/.env to the line entra.sh prints, and BACKUP_BLOB_ENDPOINT and"
      log_info "     BACKUP_CONTAINER to the lines provision.sh prints; the nightly backup also needs the public certificate of backup-recipient"
      ;;
  esac
  log_info "  3. delete the local copy: shred -u $OUT_FILE   (rm -P on macOS; on Windows delete it from a volume without shadow copies)"
  log_info "  4. bash scripts/azure/verify.sh --entra"
}

print_recipient_steps() {
  log_info "next steps:"
  log_info "  1. copy the file to the server's administrator, for example: scp $OUT_FILE <administrator>@<server>:~/$RECIPIENT_FILE"
  log_info "  2. on the server, as the administrator, place it as $SERVER_SECRETS_DIR/$RECIPIENT_FILE with the host bootstrap's"
  log_info "     --backup-recipient ~/$RECIPIENT_FILE (docs/operations/runbooks/host-setup.md), then rm ~/$RECIPIENT_FILE"
  log_info "     (it holds no key: the server seals every archive to it, and only the private key in $ERP_AZURE_KEYVAULT_OPS_NAME opens one)"
  log_info "  3. on the server, as the administrator, compare its SHA-256 fingerprint with the one printed above:"
  log_info "     sudo openssl x509 -in $SERVER_SECRETS_DIR/$RECIPIENT_FILE -noout -fingerprint -sha256"
  log_info "  4. delete the local copy: rm $OUT_FILE"
}

print_recipient_key_steps() {
  log_info "next steps:"
  log_info "  1. for a restore only, copy it to the server as the deploy account over SSH, for example:"
  log_info "     scp -i ~/.ssh/erp-ai-pro-deploy $OUT_FILE erp-ai-pro@<server>:~/$RECIPIENT_KEY_FILE"
  log_info "  2. on the server, as erp-ai-pro, list the archives, then check the one to restore, which changes nothing:"
  log_info "     bash $SERVER_CHECKOUT/scripts/deploy/restore.sh --list"
  log_info "     bash $SERVER_CHECKOUT/scripts/deploy/restore.sh --name <archive> --key ~/$RECIPIENT_KEY_FILE --dry-run"
  log_info "     then stop the stack and restore that archive by its name (docs/operations/runbooks/restore.md), which shreds the key once it"
  log_info "     is done; never leave it on the server"
  log_info "  3. delete the local copy: shred -u $OUT_FILE   (rm -P on macOS; on Windows delete it from a volume without shadow copies)"
}

main() {
  parse_export_args "$@"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az openssl
  require_ignored_destination

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  umask 077
  mkdir -p -- "$(dirname -- "$OUT_FILE")"
  trap discard_partial_download EXIT
  DOWNLOAD_STARTED=1
  download
  chmod 600 -- "$OUT_FILE"
  log_info "saved to $OUT_FILE (mode 600; on Windows the file inherits the folder's permissions instead, so keep it inside your user profile)"
  validate
  DOWNLOAD_STARTED=0

  log_step "Summary"
  printf 'CERTIFICATE_FILE=%s\n' "$OUT_FILE"
  case "$TARGET" in
    backup-recipient) print_recipient_steps ;;
    backup-recipient-key) print_recipient_key_steps ;;
    *) print_client_certificate_steps ;;
  esac
}

main "$@"
