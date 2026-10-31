#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/entra.sh [--dry-run] [--params <file>]
       bash scripts/azure/entra.sh --rotate-signin-certificate <local-dev|production> [--dry-run] [--params <file>]
       bash scripts/azure/entra.sh --rotate-client-certificate <runtime|migrator|backup> [--dry-run] [--params <file>]
       bash scripts/azure/entra.sh --rotate-encryption-certificate [--dry-run] [--params <file>]
       bash scripts/azure/entra.sh --prune-old-credentials <local-dev|production|runtime|migrator|backup> [--dry-run] [--params <file>]

Creates or converges the five single-tenant app registrations (sign-in for
local-dev, sign-in for production, and runtime, migrator and backup for the
server), their service principals, app roles, the login_hint optional claim and
the front-channel logout URL of the sign-in registrations (for an https web
origin only), Microsoft Graph consent, Key Vault certificates (the client
certificates of runtime, migrator and backup, and the RSA 4096 backup
encryption certificate the server seals its backups to, all in the operations
vault, which no application reads), key credentials, the Erp.Admin assignment
of the operator and the App Configuration identity ids and web origins, with
the production label'"'"'s Erp:Platform:Host:AllowedHosts taken from its web
origin (<address>;api;127.0.0.1). Safe to run repeatedly; never deletes a
registration.

Options:
  --dry-run                              Print every write that would run; write nothing.
  --clear-production-redirect-uris       Allow the run to remove the production redirect URIs when ERP_AZURE_PRODUCTION_WEB_ORIGIN is empty.
  --params <file>                        Parameter file (default: scripts/azure/params.env).
  --rotate-signin-certificate <label>    Create a new version of the sign-in certificate of local-dev or production and register it.
  --rotate-client-certificate <target>   Create a new version of the runtime, migrator or backup client certificate and register it.
  --rotate-encryption-certificate        Create a new version, with a new key, of the backup encryption certificate.
  --prune-old-credentials <target>       Delete every key credential of local-dev, production, runtime, migrator or backup that is not the current certificate.
  --help                                 Show this help.'

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/appconfig.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/appconfig.sh"
# shellcheck source=lib/graph.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/graph.sh"

MODE=converge
TARGET=''

set_mode() {
  [[ "$MODE" == converge ]] || die "only one of --rotate-signin-certificate, --rotate-client-certificate, --rotate-encryption-certificate and --prune-old-credentials can be given"
  MODE="$1"
}

require_target() {
  local option="$1" value="$2" accepted="$3"
  [[ -n "$value" ]] || die "$option requires a value"
  local label
  if [[ "$accepted" != server ]]; then
    for label in "${SIGNIN_LABELS[@]}"; do
      if [[ "$value" == "$label" ]]; then
        printf '%s' "$value"
        return 0
      fi
    done
  fi
  if [[ "$accepted" != signin ]] && is_server_target "$value"; then
    printf '%s' "$value"
    return 0
  fi
  case "$accepted" in
    signin) die "$option accepts local-dev or production; got '$value'" ;;
    server) die "$option accepts runtime, migrator or backup; got '$value'" ;;
    *) die "$option accepts local-dev, production, runtime, migrator or backup; got '$value'" ;;
  esac
}

parse_entra_args() {
  local -a rest=()
  while (( $# > 0 )); do
    case "$1" in
      --rotate-signin-certificate)
        set_mode rotate-signin
        TARGET="$(require_target "$1" "${2:-}" signin)"
        shift
        ;;
      --rotate-signin-certificate=*)
        set_mode rotate-signin
        TARGET="$(require_target "--rotate-signin-certificate" "${1#--rotate-signin-certificate=}" signin)"
        ;;
      --rotate-client-certificate)
        set_mode rotate-client
        TARGET="$(require_target "$1" "${2:-}" server)"
        shift
        ;;
      --rotate-client-certificate=*)
        set_mode rotate-client
        TARGET="$(require_target "--rotate-client-certificate" "${1#--rotate-client-certificate=}" server)"
        ;;
      --rotate-encryption-certificate)
        set_mode rotate-encryption
        ;;
      --clear-production-redirect-uris)
        CLEAR_REDIRECT_URIS_ALLOWED=1
        ;;
      --prune-old-credentials)
        set_mode prune
        TARGET="$(require_target "$1" "${2:-}" any)"
        shift
        ;;
      --prune-old-credentials=*)
        set_mode prune
        TARGET="$(require_target "--prune-old-credentials" "${1#--prune-old-credentials=}" any)"
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

split_ids() {
  local object_id_variable="$1" app_id_variable="$2" ids="$3"
  printf -v "$object_id_variable" '%s' "${ids%%$'\t'*}"
  printf -v "$app_id_variable" '%s' "${ids#*$'\t'}"
}

require_existing_app() {
  local display_name="$1"
  local found
  found="$(find_app "$display_name")" || exit 1
  [[ -n "$found" ]] || die "app registration '$display_name' does not exist; run: bash scripts/azure/entra.sh"
  printf '%s' "$found"
}

converge_signin_app() {
  local label="$1" operator_id="$2" admin_role_id="$3" app_id_variable="$4"
  local display_name vault common_name
  display_name="$(signin_app_name "$label")"
  vault="$(signin_vault "$label")"
  common_name="$(signin_certificate_common_name "$label")"
  local uris
  uris="$(signin_redirect_uris "$label")" || die "cannot compute the redirect URIs of the $label registration"
  local -a redirect_uris=()
  local uri
  while IFS= read -r uri; do
    [[ -n "$uri" ]] || continue
    redirect_uris+=("$uri")
  done <<< "$uris"

  local web_origin logout_url
  web_origin="$(signin_web_origin "$label")" || die "cannot compute the web origin of the $label registration"
  logout_url="$(signin_logout_url "$label")" || die "cannot compute the front-channel logout URL of the $label registration"

  log_step "Sign-in registration '$display_name' ($label)"
  if [[ "$label" == production ]] && (( ${#redirect_uris[@]} == 0 )); then
    log_warn "ERP_AZURE_PRODUCTION_WEB_ORIGIN is empty: no redirect URI is requested for production and $IDENTITY_WEB_ORIGIN_KEY [production] is not written, so its sign-in stays impossible until the parameter is set and entra.sh re-run"
  fi
  if [[ -n "$web_origin" && -z "$logout_url" ]]; then
    log_info "web origin $web_origin is not https, so no front-channel logout URL is registered for $label"
  fi
  local ids object_id app_id
  ids="$(ensure_app "$display_name" web "$logout_url" "${redirect_uris[@]}")"
  split_ids object_id app_id "$ids"
  ensure_app_roles "$object_id"
  ensure_required_resource_access "$object_id"
  local sp_id
  sp_id="$(ensure_service_principal "$app_id")"
  ensure_assignment_required "$sp_id"
  ensure_permission_grant "$app_id" "$sp_id"
  ensure_application_permission_grant "$app_id" "$sp_id"
  local thumbprint
  thumbprint="$(ensure_certificate "$vault" "$SIGNIN_CERTIFICATE_NAME" "$common_name" "$SIGNIN_CERTIFICATE_CONTENT_TYPE")"
  ensure_key_credential "$app_id" "$vault" "$SIGNIN_CERTIFICATE_NAME" "$thumbprint"
  ensure_app_role_assignment "$sp_id" "$operator_id" "$admin_role_id"
  ensure_kv "$IDENTITY_TENANT_ID_KEY" "$label" "$ERP_AZURE_TENANT_ID"
  ensure_kv "$IDENTITY_CLIENT_ID_KEY" "$label" "$app_id"
  if [[ -n "$web_origin" ]]; then
    ensure_kv "$IDENTITY_WEB_ORIGIN_KEY" "$label" "$web_origin"
  fi
  if [[ "$label" == production && -n "$web_origin" ]]; then
    ensure_kv "$HOST_ALLOWED_HOSTS_KEY" "$label" "$(production_allowed_hosts)"
  fi
  printf -v "$app_id_variable" '%s' "$app_id"
}

converge_server_app() {
  local target="$1" app_id_variable="$2"
  local display_name certificate_name
  display_name="$(server_app_name "$target")"
  certificate_name="$(client_certificate_name "$target")"
  log_step "Server registration '$display_name' ($target)"
  local ids object_id app_id
  ids="$(ensure_app "$display_name" none '')"
  split_ids object_id app_id "$ids"
  ensure_service_principal "$app_id" > /dev/null
  local thumbprint
  thumbprint="$(ensure_certificate "$ERP_AZURE_KEYVAULT_OPS_NAME" "$certificate_name" "$(client_certificate_common_name "$target")" "$CLIENT_CERTIFICATE_CONTENT_TYPE")"
  ensure_key_credential "$app_id" "$ERP_AZURE_KEYVAULT_OPS_NAME" "$certificate_name" "$thumbprint"
  printf -v "$app_id_variable" '%s' "$app_id"
}

converge_backup_encryption_certificate() {
  log_step "Backup encryption certificate '$BACKUP_ENCRYPTION_CERTIFICATE_NAME'"
  ensure_certificate "$ERP_AZURE_KEYVAULT_OPS_NAME" "$BACKUP_ENCRYPTION_CERTIFICATE_NAME" "$BACKUP_ENCRYPTION_CERTIFICATE_COMMON_NAME" \
    "$CLIENT_CERTIFICATE_CONTENT_TYPE" "$ENCRYPTION_CERTIFICATE_POLICY_TEMPLATE" > /dev/null
}

run_converge() {
  require_origin ERP_AZURE_LOCAL_WEB_ORIGIN "$ERP_AZURE_LOCAL_WEB_ORIGIN"
  [[ -z "$ERP_AZURE_PRODUCTION_WEB_ORIGIN" ]] || require_origin ERP_AZURE_PRODUCTION_WEB_ORIGIN "$ERP_AZURE_PRODUCTION_WEB_ORIGIN"
  log_step "Resolving the operator and Microsoft Graph"
  local operator_id admin_role_id
  operator_id="$(signed_in_user_object_id)"
  log_info "operator: $operator_id"
  resolve_graph_permission_ids
  log_info "Microsoft Graph service principal: $GRAPH_SP_OBJECT_ID"
  admin_role_id="$(app_role_id "$APP_ROLE_ADMIN_VALUE")"

  local local_dev_app_id='' production_app_id='' runtime_app_id='' migrator_app_id='' backup_app_id=''
  converge_signin_app local-dev "$operator_id" "$admin_role_id" local_dev_app_id
  converge_signin_app production "$operator_id" "$admin_role_id" production_app_id
  converge_server_app runtime runtime_app_id
  converge_server_app migrator migrator_app_id
  converge_server_app backup backup_app_id
  converge_backup_encryption_certificate

  log_step "Summary"
  printf 'LOCAL_DEV_CLIENT_ID=%s\n' "$local_dev_app_id"
  printf 'PRODUCTION_CLIENT_ID=%s\n' "$production_app_id"
  printf 'RUNTIME_CLIENT_ID=%s\n' "$runtime_app_id"
  printf 'MIGRATOR_CLIENT_ID=%s\n' "$migrator_app_id"
  printf 'BACKUP_CLIENT_ID=%s\n' "$backup_app_id"
  printf 'AZURE_TENANT_ID=%s\n' "$ERP_AZURE_TENANT_ID"
  printf 'AZURE_CLIENT_ID=%s\n' "$runtime_app_id"
  printf 'MIGRATOR_AZURE_CLIENT_ID=%s\n' "$migrator_app_id"
  printf 'BACKUP_AZURE_CLIENT_ID=%s\n' "$backup_app_id"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed; ids shown as $PENDING_ID are assigned by the real run"
  fi
  log_info "AZURE_TENANT_ID, AZURE_CLIENT_ID (the runtime's), MIGRATOR_AZURE_CLIENT_ID and BACKUP_AZURE_CLIENT_ID are the values the server puts in infra/compose/.env (docs/operations/runbooks/host-setup.md)"
  log_info "next steps:"
  log_info "  1. bash scripts/azure/provision.sh   (grants the runtime, migrator and backup service principals their roles)"
  log_info "  2. create Erp--Platform--Attachments--EncryptionKey in $ERP_AZURE_KEYVAULT_DEV_NAME and in $ERP_AZURE_KEYVAULT_PROD_NAME once each (docs/operations/runbooks/secrets.md, section 5f)"
  log_info "  3. bash scripts/azure/seed.sh        (configuration values and Key Vault references)"
  log_info "  4. $HOME_ADDRESS_HINT   (docs/operations/runbooks/home-address.md),"
  log_info "     then bash scripts/azure/database.sh   (the database users; docs/operations/runbooks/database.md)"
  log_info "  5. the first migration, with bash scripts/azure/export-client-certificate.sh migrator (docs/operations/runbooks/database.md)"
  log_info "  6. bash scripts/azure/verify.sh --entra"
  log_info "  7. for the server's nightly backup: bash scripts/azure/export-client-certificate.sh backup and backup-recipient (docs/operations/runbooks/host-setup.md)"

  if (( ${#PERMISSION_GRANT_FAILURES[@]} > 0 )); then
    local app_id
    for app_id in "${PERMISSION_GRANT_FAILURES[@]}"; do
      log_error "Graph consent is missing for $app_id; as an administrator run: az ad app permission admin-consent --id $app_id"
    done
    die "${#PERMISSION_GRANT_FAILURES[@]} consent grant(s) failed; every other step completed"
  fi
}

run_rotate_signin() {
  local label="$TARGET"
  local display_name vault common_name
  display_name="$(signin_app_name "$label")"
  vault="$(signin_vault "$label")"
  common_name="$(signin_certificate_common_name "$label")"

  log_step "Rotating the sign-in certificate of '$display_name' ($label)"
  local ids object_id app_id
  ids="$(require_existing_app "$display_name")"
  split_ids object_id app_id "$ids"
  local thumbprint
  thumbprint="$(rotate_certificate "$vault" "$SIGNIN_CERTIFICATE_NAME" "$common_name" "$SIGNIN_CERTIFICATE_CONTENT_TYPE")"
  ensure_key_credential "$app_id" "$vault" "$SIGNIN_CERTIFICATE_NAME" "$thumbprint"

  log_step "Summary"
  printf 'CLIENT_ID=%s\n' "$app_id"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed"
  fi
  log_info "next steps:"
  if [[ "$label" == production ]]; then
    log_info "  1. restart the api so it loads the new certificate (it reads the certificate only when it starts), on the server:"
    log_info "     flock /srv/erp-ai-pro/deploy.lock docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up --detach --wait --wait-timeout 300 --force-recreate --no-deps --no-build --pull never api"
  else
    log_info "  1. restart every API started with dotnet run so it loads the new certificate from the store (it reads the certificate only when it"
    log_info "     starts); the local containers and the store-less APIs of the browser tests use throwaway certificates and need nothing"
  fi
  log_info "  2. sign in through the $label web app and confirm it works"
  log_info "  3. bash scripts/azure/entra.sh --prune-old-credentials $label"
}

run_rotate_client() {
  local display_name certificate_name
  display_name="$(server_app_name "$TARGET")"
  certificate_name="$(client_certificate_name "$TARGET")"
  log_step "Rotating the $TARGET client certificate of '$display_name'"
  local ids object_id app_id
  ids="$(require_existing_app "$display_name")"
  split_ids object_id app_id "$ids"
  local thumbprint
  thumbprint="$(rotate_certificate "$ERP_AZURE_KEYVAULT_OPS_NAME" "$certificate_name" "$(client_certificate_common_name "$TARGET")" "$CLIENT_CERTIFICATE_CONTENT_TYPE")"
  ensure_key_credential "$app_id" "$ERP_AZURE_KEYVAULT_OPS_NAME" "$certificate_name" "$thumbprint"

  log_step "Summary"
  printf 'CLIENT_ID=%s\n' "$app_id"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed"
  fi
  log_info "next steps:"
  log_info "  1. bash scripts/azure/export-client-certificate.sh $TARGET   (downloads the new version and prints the placement command for the server)"
  case "$TARGET" in
    runtime)
      log_info "  2. on the server: flock /srv/erp-ai-pro/deploy.lock docker compose -f infra/compose/compose.yaml -f infra/compose/compose.production.yaml up --detach --wait --wait-timeout 300 --force-recreate --no-deps --no-build --pull never api   (a plain up -d keeps the running container with the old certificate file)"
      ;;
    migrator)
      log_info "  2. nothing to restart: every migrator run, the nightly retain included, starts a new container that reads the new file"
      ;;
    backup)
      log_info "  2. nothing to restart: every backup run starts a new container that reads the new file"
      ;;
  esac
  log_info "  3. bash scripts/azure/entra.sh --prune-old-credentials $TARGET"
  log_info "  4. bash scripts/azure/verify.sh --entra"
}

run_rotate_encryption() {
  log_step "Rotating the backup encryption certificate '$BACKUP_ENCRYPTION_CERTIFICATE_NAME'"
  local thumbprint
  thumbprint="$(rotate_certificate "$ERP_AZURE_KEYVAULT_OPS_NAME" "$BACKUP_ENCRYPTION_CERTIFICATE_NAME" "$BACKUP_ENCRYPTION_CERTIFICATE_COMMON_NAME" \
    "$CLIENT_CERTIFICATE_CONTENT_TYPE" "$ENCRYPTION_CERTIFICATE_POLICY_TEMPLATE")"

  log_step "Summary"
  printf 'ENCRYPTION_CERTIFICATE_THUMBPRINT=%s\n' "$thumbprint"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed"
  fi
  log_info "next steps:"
  log_info "  1. bash scripts/azure/export-client-certificate.sh backup-recipient   (the new public certificate; place it on the server in place of the old one, docs/operations/runbooks/secrets.md section 5g)"
  log_info "  2. keep every earlier version enabled until the newest archive sealed to it is past its 90 days and the newest copy of the journal sealed to it is past its 365 days,"
  log_info "     in practice until the next rotation: a restore of such an archive, or journal.sh --fetch of such a copy, exports that version with"
  log_info "     bash scripts/azure/export-client-certificate.sh backup-recipient-key --version <version>   (az keyvault certificate list-versions --vault-name $ERP_AZURE_KEYVAULT_OPS_NAME --name $BACKUP_ENCRYPTION_CERTIFICATE_NAME lists them)"
  log_info "  3. bash scripts/azure/verify.sh --entra"
}

run_prune() {
  local display_name vault certificate_name
  if is_server_target "$TARGET"; then
    display_name="$(server_app_name "$TARGET")"
    vault="$ERP_AZURE_KEYVAULT_OPS_NAME"
    certificate_name="$(client_certificate_name "$TARGET")"
  else
    display_name="$(signin_app_name "$TARGET")"
    vault="$(signin_vault "$TARGET")"
    certificate_name="$SIGNIN_CERTIFICATE_NAME"
  fi

  log_step "Pruning old key credentials of '$display_name' ($TARGET)"
  local ids object_id app_id
  ids="$(require_existing_app "$display_name")"
  split_ids object_id app_id "$ids"
  local thumbprint
  thumbprint="$(certificate_thumbprint "$vault" "$certificate_name")" || die "cannot read certificate '$certificate_name' in vault '$vault'"
  [[ -n "$thumbprint" ]] || die "certificate '$certificate_name' does not exist in vault '$vault'; run: bash scripts/azure/entra.sh"
  log_info "current certificate thumbprint: $thumbprint"
  prune_key_credentials "$app_id" "$thumbprint"

  log_step "Summary"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was deleted"
  fi
  log_info "next step: bash scripts/azure/verify.sh --entra"
}

main() {
  parse_entra_args "$@"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az node mktemp

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  case "$MODE" in
    converge) run_converge ;;
    rotate-signin) run_rotate_signin ;;
    rotate-client) run_rotate_client ;;
    rotate-encryption) run_rotate_encryption ;;
    prune) run_prune ;;
    *) die "unknown mode '$MODE'" ;;
  esac
}

main "$@"
