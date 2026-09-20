#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/export-runtime-certificate.sh [--out <file>] [--params <file>]

Downloads the runtime client certificate (PEM with the private key) from the
production Key Vault into a local file for a one-time copy to the server,
validates it with openssl and prints the placement command. Refuses to
overwrite an existing file and never prints the content.

Options:
  --out <file>      Destination file, relative to the repository root (default: scripts/azure/out/erp-runtime-client.pem, gitignored).
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/graph.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/graph.sh"

readonly DEFAULT_OUT_FILE='scripts/azure/out/erp-runtime-client.pem'
readonly SERVER_SECRETS_DIR='infra/compose/secrets'
readonly API_CONTAINER_USER_PLACEHOLDER='<api container uid/gid>'

OUT_FILE="$DEFAULT_OUT_FILE"

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
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
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

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  log_step "Downloading $RUNTIME_CERTIFICATE_NAME from $ERP_AZURE_KEYVAULT_PROD_NAME"
  mkdir -p -- "$(dirname -- "$OUT_FILE")"
  umask 077
  trap discard_partial_download EXIT
  DOWNLOAD_STARTED=1
  az keyvault secret download --vault-name "$ERP_AZURE_KEYVAULT_PROD_NAME" --name "$RUNTIME_CERTIFICATE_NAME" \
    --file "$OUT_FILE" --only-show-errors
  chmod 600 -- "$OUT_FILE"
  log_info "saved to $OUT_FILE (mode 600)"

  log_step "Validating with openssl"
  openssl x509 -in "$OUT_FILE" -noout -subject -enddate || discard_invalid_file
  openssl rsa -in "$OUT_FILE" -check -noout || discard_invalid_file
  log_info "certificate and RSA private key are valid"
  DOWNLOAD_STARTED=0

  log_step "Summary"
  printf 'RUNTIME_CERTIFICATE_FILE=%s\n' "$OUT_FILE"
  log_info "next steps:"
  log_info "  1. copy the file to the server over an encrypted channel, for example: scp $OUT_FILE <user>@<server>:~/erp-runtime-client.pem"
  log_info "  2. on the server, from the repository root:"
  log_info "     mkdir -p $SERVER_SECRETS_DIR"
  log_info "     install -o $API_CONTAINER_USER_PLACEHOLDER -g $API_CONTAINER_USER_PLACEHOLDER -m 0400 ~/erp-runtime-client.pem $SERVER_SECRETS_DIR/erp-runtime-client.pem"
  log_info "     rm -f ~/erp-runtime-client.pem"
  log_info "     (the uid and gid are those of the API container user, recorded by sub-phase azure-configuration-api-configuration-bootstrap)"
  log_info "  3. delete the local copy: rm -f $OUT_FILE"
  log_info "  4. bash scripts/azure/verify.sh --entra"
}

main "$@"
