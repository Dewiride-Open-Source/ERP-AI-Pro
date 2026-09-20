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
readonly API_CONTAINER_USER='1654'

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
  [[ "$OUT_FILE" == *.pem ]] || die "--out must end in .pem so the file stays covered by .gitignore; got '$OUT_FILE'"
  if git -C "$REPO_ROOT" rev-parse --is-inside-work-tree > /dev/null 2>&1 && ! git -C "$REPO_ROOT" check-ignore -q -- "$OUT_FILE"; then
    die "--out '$OUT_FILE' is inside the repository but not ignored by git; use the default location or a path under scripts/azure/out/"
  fi

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  log_step "Downloading $RUNTIME_CERTIFICATE_NAME from $ERP_AZURE_KEYVAULT_PROD_NAME"
  umask 077
  mkdir -p -- "$(dirname -- "$OUT_FILE")"
  trap discard_partial_download EXIT
  DOWNLOAD_STARTED=1
  az keyvault secret download --vault-name "$ERP_AZURE_KEYVAULT_PROD_NAME" --name "$RUNTIME_CERTIFICATE_NAME" \
    --file "$OUT_FILE" --only-show-errors
  chmod 600 -- "$OUT_FILE"
  log_info "saved to $OUT_FILE (mode 600; on Windows the file inherits the folder's permissions instead, so keep it inside your user profile)"

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
  log_info "     install -d -m 0700 $SERVER_SECRETS_DIR"
  log_info "     install -o $API_CONTAINER_USER -g $API_CONTAINER_USER -m 0400 ~/erp-runtime-client.pem $SERVER_SECRETS_DIR/erp-runtime-client.pem"
  log_info "     shred -u ~/erp-runtime-client.pem"
  log_info "     ($API_CONTAINER_USER is the uid and gid of the app user in the chiseled aspnet image the api container runs as)"
  log_info "  3. shred the local copy: shred -u $OUT_FILE   (rm -P on macOS; on Windows delete it from a volume without shadow copies)"
  log_info "  4. bash scripts/azure/verify.sh --entra"
}

main "$@"
