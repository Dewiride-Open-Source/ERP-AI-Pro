#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/export-client-certificate.sh <runtime|migrator> [--out <file>] [--params <file>]

Downloads the client certificate (PEM with the private key) of the runtime or
the migrator service principal from the operations Key Vault into a local file
for a one-time copy to the server, validates it with openssl and prints the
placement command. Refuses to overwrite an existing file and never prints the
content.

Options:
  --out <file>      Destination file, relative to the repository root (default: scripts/azure/out/erp-<runtime|migrator>-client.pem, gitignored).
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/graph.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/graph.sh"

readonly SERVER_SECRETS_DIR='/srv/erp-ai-pro/ERP-AI-Pro/infra/compose/secrets'
readonly CONTAINER_USER='1654'

TARGET=''
OUT_FILE=''

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
      *)
        rest+=("$1")
        ;;
    esac
    shift
  done
  parse_args "${rest[@]}"
  (( ${#ARGS[@]} == 1 )) || die "name the certificate to export: ${SERVER_TARGETS[*]}"
  require_server_target "${ARGS[0]}"
  TARGET="${ARGS[0]}"
  [[ -n "$OUT_FILE" ]] || OUT_FILE="$ERP_AZURE_TEMP_DIR/$(client_certificate_file "$TARGET")"
}

DOWNLOAD_STARTED=0

discard_invalid_file() {
  rm -f -- "$OUT_FILE"
  die "the downloaded secret is not a PEM certificate with an unencrypted RSA private key (see the openssl error above); $OUT_FILE was deleted"
}

discard_partial_download() {
  if (( DOWNLOAD_STARTED )) && [[ -e "$OUT_FILE" ]]; then
    rm -f -- "$OUT_FILE"
    log_warn "$OUT_FILE was deleted because the export did not complete"
  fi
}

main() {
  parse_export_args "$@"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az openssl
  [[ ! -e "$OUT_FILE" ]] || die "$OUT_FILE already exists; delete it or choose another --out path (the file is never overwritten)"
  [[ "$OUT_FILE" == *.pem ]] || die "--out must end in .pem so the file stays covered by .gitignore; got '$OUT_FILE'"
  if git -C "$REPO_ROOT" rev-parse --is-inside-work-tree > /dev/null 2>&1 && ! git -C "$REPO_ROOT" check-ignore -q -- "$OUT_FILE"; then
    die "--out '$OUT_FILE' is inside the repository but not ignored by git; use the default location or a path under $ERP_AZURE_TEMP_DIR/"
  fi

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  local secret_name file_name
  secret_name="$(client_certificate_name "$TARGET")"
  file_name="$(client_certificate_file "$TARGET")"
  log_step "Downloading $secret_name from $ERP_AZURE_KEYVAULT_OPS_NAME"
  umask 077
  mkdir -p -- "$(dirname -- "$OUT_FILE")"
  trap discard_partial_download EXIT
  DOWNLOAD_STARTED=1
  az keyvault secret download --vault-name "$ERP_AZURE_KEYVAULT_OPS_NAME" --name "$secret_name" \
    --file "$OUT_FILE" --only-show-errors
  chmod 600 -- "$OUT_FILE"
  log_info "saved to $OUT_FILE (mode 600; on Windows the file inherits the folder's permissions instead, so keep it inside your user profile)"

  log_step "Validating with openssl"
  openssl x509 -in "$OUT_FILE" -noout -subject -enddate || discard_invalid_file
  openssl rsa -in "$OUT_FILE" -check -noout || discard_invalid_file
  log_info "certificate and RSA private key are valid"
  DOWNLOAD_STARTED=0

  log_step "Summary"
  printf 'CERTIFICATE_FILE=%s\n' "$OUT_FILE"
  log_info "next steps:"
  log_info "  1. copy the file to the server's administrator over an encrypted channel, for example: scp $OUT_FILE <administrator>@<server>:~/$file_name"
  log_info "  2. on the server, as the administrator (the host bootstrap made the folder, docs/operations/runbooks/host-setup.md):"
  log_info "     sudo install -o $CONTAINER_USER -g $CONTAINER_USER -m 0400 ~/$file_name $SERVER_SECRETS_DIR/$file_name"
  log_info "     shred -u ~/$file_name"
  log_info "     ($CONTAINER_USER is the uid and gid of the app user in the chiseled aspnet image the api and migrator containers run as)"
  if [[ "$TARGET" == migrator ]]; then
    log_info "     then set MIGRATOR_AZURE_CLIENT_ID in /srv/erp-ai-pro/ERP-AI-Pro/infra/compose/.env to the MIGRATOR_AZURE_CLIENT_ID line entra.sh prints;"
    log_info "     from then on erp-ai-pro-retain.timer runs retain every night, so place it only once the deployment's images are on the server"
  fi
  log_info "  3. delete the local copy: shred -u $OUT_FILE   (rm -P on macOS; on Windows delete it from a volume without shadow copies)"
  log_info "  4. bash scripts/azure/verify.sh --entra"
}

main "$@"
