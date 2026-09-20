#!/usr/bin/env bash

if [[ -n "${ERP_AZURE_GRAPH_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_AZURE_GRAPH_SH_LOADED=1

# shellcheck source=common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"

readonly GRAPH_BASE_URL='https://graph.microsoft.com/v1.0'
readonly GRAPH_APP_ID='00000003-0000-0000-c000-000000000000'
readonly GRAPH_SCOPE_VALUES=(openid profile User.Read)
readonly PENDING_ID='<pending>'
readonly APP_ROLES_FILE='scripts/azure/entra/app-roles.json'
readonly CERTIFICATE_POLICY_TEMPLATE='scripts/azure/entra/certificate-policy.json'
readonly ENTRA_TEMP_DIR='scripts/azure/out'
readonly APP_ROLE_ADMIN_VALUE='Erp.Admin'
readonly APP_ROLE_USER_VALUE='Erp.User'
readonly SIGNIN_LABELS=(local-dev production)
readonly SIGNIN_CERTIFICATE_NAME='Erp--Platform--Identity--ClientCertificate'
readonly SIGNIN_CERTIFICATE_CONTENT_TYPE='application/x-pkcs12'
readonly RUNTIME_CERTIFICATE_NAME='Erp--Platform--Identity--RuntimeClientCertificate'
readonly RUNTIME_CERTIFICATE_CONTENT_TYPE='application/x-pem-file'
readonly RUNTIME_CERTIFICATE_COMMON_NAME='erp-ai-pro-runtime-production'
readonly IDENTITY_TENANT_ID_KEY='Erp:Platform:Identity:TenantId'
readonly IDENTITY_CLIENT_ID_KEY='Erp:Platform:Identity:ClientId'
readonly REDIRECT_PATHS=(/api/auth/signin-oidc /api/auth/signout-callback-oidc)
readonly CERTIFICATE_EXPIRY_WARNING_SECONDS=$((30 * 24 * 3600))

GRAPH_SP_OBJECT_ID=''
GRAPH_SCOPE_OPENID=''
GRAPH_SCOPE_PROFILE=''
GRAPH_SCOPE_USER_READ=''
PERMISSION_GRANT_FAILURES=()
CERTIFICATE_POLICY_FILE=''

is_pending() {
  [[ "$1" == "$PENDING_ID" ]]
}

az_error_summary() {
  printf '%s' "$1" | tr -d '\r' | sed -n '/[^[:space:]]/p' | tail -n 1
}

log_change() {
  local subject="$1" verb="$2" suffix="${3:-}"
  if (( DRY_RUN )); then
    verb="would be $verb"
  fi
  log_info "$subject $verb$suffix"
}

require_repo_root_cwd() {
  [[ "$PWD" == "$REPO_ROOT" ]] || die "the current directory must be the repository root ($REPO_ROOT) so that az can open @file arguments"
}

base64_to_hex() {
  [[ -n "$1" ]] || return 0
  { printf '%s' "$1" | base64 -d 2> /dev/null | od -An -tx1 | tr -d ' \n'; } || true
}

require_origin() {
  local name="$1" value="$2"
  if [[ "$value" =~ ^https://[^/[:space:]]+$ ]] || [[ "$value" =~ ^http://(localhost|127\.0\.0\.1)(:[0-9]+)?$ ]]; then
    return 0
  fi
  die "$name must be an https origin without a path or trailing slash (http is accepted for localhost only); got '$value'"
}

require_signin_label() {
  local label
  for label in "${SIGNIN_LABELS[@]}"; do
    [[ "$1" == "$label" ]] && return 0
  done
  die "unknown sign-in label '$1'; expected one of: ${SIGNIN_LABELS[*]}"
}

signin_app_name() {
  require_signin_label "$1"
  if [[ "$1" == local-dev ]]; then
    printf '%s' "$ERP_AZURE_APP_WEB_LOCAL_DEV_NAME"
  else
    printf '%s' "$ERP_AZURE_APP_WEB_PRODUCTION_NAME"
  fi
}

signin_vault() {
  require_signin_label "$1"
  if [[ "$1" == local-dev ]]; then
    printf '%s' "$ERP_AZURE_KEYVAULT_DEV_NAME"
  else
    printf '%s' "$ERP_AZURE_KEYVAULT_PROD_NAME"
  fi
}

signin_certificate_common_name() {
  require_signin_label "$1"
  printf 'erp-ai-pro-web-%s' "$1"
}

signin_redirect_uris() {
  local label="$1"
  require_signin_label "$label"
  local -a origins=()
  if [[ "$label" == local-dev ]]; then
    require_origin ERP_AZURE_LOCAL_WEB_ORIGIN "$ERP_AZURE_LOCAL_WEB_ORIGIN"
    require_origin ERP_AZURE_LOCAL_API_ORIGIN "$ERP_AZURE_LOCAL_API_ORIGIN"
    origins=("$ERP_AZURE_LOCAL_WEB_ORIGIN" "$ERP_AZURE_LOCAL_API_ORIGIN")
  else
    [[ -n "$ERP_AZURE_PRODUCTION_WEB_ORIGIN" ]] || return 0
    require_origin ERP_AZURE_PRODUCTION_WEB_ORIGIN "$ERP_AZURE_PRODUCTION_WEB_ORIGIN"
    origins=("$ERP_AZURE_PRODUCTION_WEB_ORIGIN")
  fi
  local origin path
  for origin in "${origins[@]}"; do
    for path in "${REDIRECT_PATHS[@]}"; do
      printf '%s%s\n' "$origin" "$path"
    done
  done
}

app_role_id() {
  local value="$1"
  require_repo_root_cwd
  local id
  id="$(node -e 'const roles = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8")); const role = roles.find((candidate) => candidate.value === process.argv[2]); process.stdout.write(role ? role.id : "");' "$APP_ROLES_FILE" "$value")"
  [[ -n "$id" ]] || die "app role '$value' is not defined in $APP_ROLES_FILE"
  printf '%s' "$id"
}

graph_get() {
  (( $# >= 1 && $# <= 3 )) || die "graph_get: usage graph_get <path> [<jmespath-query> [<output>]]"
  local path="$1" query="${2:-}" output="${3:-}"
  local -a query_args=()
  if [[ -n "$query" ]]; then
    query_args=(--query "$query")
    [[ -n "$output" ]] || output=tsv
  fi
  [[ -n "$output" ]] || output=json
  az_read rest --method get --url "$GRAPH_BASE_URL/$path" "${query_args[@]}" --output "$output"
}

graph_write() {
  local method="$1" path="$2" body="$3"
  run az rest --method "$method" --url "$GRAPH_BASE_URL/$path" --headers 'Content-Type=application/json' \
    --body "$body" --output none --only-show-errors
}

graph_patch() {
  (( $# == 2 )) || die "graph_patch: usage graph_patch <path> <json>"
  graph_write patch "$1" "$2"
}

graph_post() {
  (( $# == 2 )) || die "graph_post: usage graph_post <path> <json>"
  graph_write post "$1" "$2"
}

find_app() {
  (( $# == 1 )) || die "find_app: usage find_app <display-name>"
  local display_name="$1"
  local rows
  rows="$(az_read ad app list --filter "displayName eq $(odata_string_literal "$display_name")" --query '[].[id, appId]' --output tsv)" \
    || die "cannot list app registrations named '$display_name'"
  [[ -n "$rows" ]] || return 0
  if (( $(printf '%s\n' "$rows" | wc -l) > 1 )); then
    die "more than one app registration is named '$display_name' (object ids: $(printf '%s\n' "$rows" | awk -F '\t' '{ printf "%s%s", (NR > 1 ? ", " : ""), $1 }')); rename or delete the duplicates"
  fi
  printf '%s' "$rows"
}

app_differences() {
  local app_json="$1"
  shift
  node -e 'let raw = ""; process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => { const app = JSON.parse(raw); const expected = process.argv.slice(1).sort(); const differences = []; if (app.signInAudience !== "AzureADMyOrg") differences.push("signInAudience"); const web = app.web ?? {}; const current = [...(web.redirectUris ?? [])].sort(); if (current.length !== expected.length || current.some((uri, index) => uri !== expected[index])) differences.push("redirectUris"); const implicit = web.implicitGrantSettings ?? {}; if (implicit.enableIdTokenIssuance === true || implicit.enableAccessTokenIssuance === true) differences.push("implicitGrant"); if ((app.api ?? {}).requestedAccessTokenVersion !== 2) differences.push("requestedAccessTokenVersion"); process.stdout.write(differences.join("\n")); });' "$@" <<< "$app_json"
}

converge_app() {
  local object_id="$1" display_name="$2"
  shift 2
  local -a redirect_uris=("$@")
  local app_json
  app_json="$(az_read ad app show --id "$object_id" --output json)" || die "cannot read app registration '$display_name'"
  local differences
  differences="$(app_differences "$app_json" "${redirect_uris[@]}")"
  if [[ -z "$differences" ]]; then
    log_info "'$display_name' unchanged"
    return 0
  fi
  local -a update_args=()
  local clear_redirect_uris=0 difference
  while IFS= read -r difference; do
    case "$difference" in
      signInAudience) update_args+=(--sign-in-audience AzureADMyOrg) ;;
      implicitGrant) update_args+=(--enable-id-token-issuance false --enable-access-token-issuance false) ;;
      requestedAccessTokenVersion) update_args+=(--requested-access-token-version 2) ;;
      redirectUris)
        if (( ${#redirect_uris[@]} > 0 )); then
          update_args+=(--web-redirect-uris "${redirect_uris[@]}")
        else
          clear_redirect_uris=1
        fi
        ;;
      *) die "converge_app: unexpected difference '$difference'" ;;
    esac
  done <<< "$differences"
  if (( ${#update_args[@]} > 0 )); then
    run az ad app update --id "$object_id" "${update_args[@]}" --output none --only-show-errors || die "cannot update app registration '$display_name'"
  fi
  if (( clear_redirect_uris )); then
    graph_patch "applications/$object_id" '{"web":{"redirectUris":[]}}' || die "cannot clear the redirect URIs of '$display_name'"
  fi
  log_change "'$display_name'" updated " ($(printf '%s\n' "$differences" | awk '{ printf "%s%s", (NR > 1 ? ", " : ""), $0 }'))"
}

ensure_app() {
  (( $# >= 2 )) || die "ensure_app: usage ensure_app <display-name> <web|none> [<redirect-uri>...]"
  local display_name="$1" kind="$2"
  shift 2
  local -a redirect_uris=("$@")
  case "$kind" in
    web) ;;
    none) (( ${#redirect_uris[@]} == 0 )) || die "ensure_app: kind none takes no redirect URIs" ;;
    *) die "ensure_app: kind must be web or none" ;;
  esac
  local found
  found="$(find_app "$display_name")" || die "cannot resolve app registration '$display_name'"
  if [[ -n "$found" ]]; then
    converge_app "${found%%$'\t'*}" "$display_name" "${redirect_uris[@]}"
    printf '%s' "$found"
    return 0
  fi
  local -a create_args=(--display-name "$display_name" --sign-in-audience AzureADMyOrg
    --enable-id-token-issuance false --enable-access-token-issuance false --requested-access-token-version 2)
  if (( ${#redirect_uris[@]} > 0 )); then
    create_args+=(--web-redirect-uris "${redirect_uris[@]}")
  fi
  if (( DRY_RUN )); then
    run az ad app create "${create_args[@]}" --query '[id, appId]' --output tsv --only-show-errors
    log_change "'$display_name'" created
    printf '%s\t%s' "$PENDING_ID" "$PENDING_ID"
    return 0
  fi
  local created
  created="$(az_read ad app create "${create_args[@]}" --query '[id, appId]' --output tsv)" || die "cannot create app registration '$display_name'"
  local object_id app_id
  object_id="$(printf '%s\n' "$created" | sed -n '1p')"
  app_id="$(printf '%s\n' "$created" | sed -n '2p')"
  if ! is_guid "$object_id" || ! is_guid "$app_id"; then
    die "unexpected response while creating app registration '$display_name'"
  fi
  log_info "'$display_name' created (application id $app_id)"
  printf '%s\t%s' "$object_id" "$app_id"
}

app_roles_verdict() {
  local current_json="$1"
  require_repo_root_cwd
  node -e 'let raw = ""; process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => { const current = JSON.parse(raw) ?? []; const desired = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8")); const key = (role) => String(role.id).toLowerCase(); const normalise = (role) => JSON.stringify({ id: key(role), value: role.value, isEnabled: role.isEnabled === true, displayName: role.displayName, description: role.description, allowedMemberTypes: [...(role.allowedMemberTypes ?? [])].sort() }); const desiredById = new Map(desired.map((role) => [key(role), normalise(role)])); const blocked = current.filter((role) => role.isEnabled === true && !desiredById.has(key(role))).map((role) => role.value); if (blocked.length > 0) { process.stdout.write("blocked " + blocked.join(", ")); return; } const currentById = new Map(current.map((role) => [key(role), normalise(role)])); const unchanged = currentById.size === desiredById.size && [...desiredById].every(([id, role]) => currentById.get(id) === role); process.stdout.write(unchanged ? "unchanged" : "update"); });' "$APP_ROLES_FILE" <<< "$current_json"
}

ensure_app_roles() {
  (( $# == 1 )) || die "ensure_app_roles: usage ensure_app_roles <object-id>"
  local object_id="$1"
  require_repo_root_cwd
  if is_pending "$object_id"; then
    run az ad app update --id "$object_id" --app-roles "@$APP_ROLES_FILE" --output none --only-show-errors
    return 0
  fi
  local current
  current="$(az_read ad app show --id "$object_id" --query appRoles --output json)" || die "cannot read the app roles of $object_id"
  local verdict
  verdict="$(app_roles_verdict "$current")"
  case "$verdict" in
    unchanged)
      log_info "app roles unchanged ($APP_ROLE_USER_VALUE, $APP_ROLE_ADMIN_VALUE)"
      ;;
    update)
      run az ad app update --id "$object_id" --app-roles "@$APP_ROLES_FILE" --output none --only-show-errors
      log_change "app roles" updated " from $APP_ROLES_FILE"
      ;;
    blocked*)
      log_warn "app roles left unchanged: ${verdict#blocked } enabled in Entra but absent from $APP_ROLES_FILE; set isEnabled false on those roles first, then re-run"
      ;;
    *) die "ensure_app_roles: unexpected verdict '$verdict'" ;;
  esac
}

resolve_graph_scope_ids() {
  local json
  json="$(az_read ad sp show --id "$GRAPH_APP_ID" --output json \
    --query "{id: id, openid: oauth2PermissionScopes[?value=='openid'].id | [0], profile: oauth2PermissionScopes[?value=='profile'].id | [0], userRead: oauth2PermissionScopes[?value=='User.Read'].id | [0]}")" \
    || die "cannot read the Microsoft Graph service principal ($GRAPH_APP_ID)"
  GRAPH_SP_OBJECT_ID="$(json_field "$json" id)"
  GRAPH_SCOPE_OPENID="$(json_field "$json" openid)"
  GRAPH_SCOPE_PROFILE="$(json_field "$json" profile)"
  GRAPH_SCOPE_USER_READ="$(json_field "$json" userRead)"
  is_guid "$GRAPH_SP_OBJECT_ID" || die "the Microsoft Graph service principal has no object id"
  is_guid "$GRAPH_SCOPE_OPENID" || die "Microsoft Graph delegated permission 'openid' not found"
  is_guid "$GRAPH_SCOPE_PROFILE" || die "Microsoft Graph delegated permission 'profile' not found"
  is_guid "$GRAPH_SCOPE_USER_READ" || die "Microsoft Graph delegated permission 'User.Read' not found"
}

required_resource_access_json() {
  printf '[{"resourceAppId":"%s","resourceAccess":[{"id":"%s","type":"Scope"},{"id":"%s","type":"Scope"},{"id":"%s","type":"Scope"}]}]' \
    "$GRAPH_APP_ID" "$GRAPH_SCOPE_OPENID" "$GRAPH_SCOPE_PROFILE" "$GRAPH_SCOPE_USER_READ"
}

required_resource_access_matches() {
  local current_json="$1"
  node -e 'let raw = ""; process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => { const current = JSON.parse(raw) ?? []; const [resourceAppId, ...scopeIds] = process.argv.slice(1).map((value) => value.toLowerCase()); const expected = scopeIds.map((id) => id + ":scope").sort().join(","); const matches = current.length === 1 && String(current[0].resourceAppId).toLowerCase() === resourceAppId && (current[0].resourceAccess ?? []).map((access) => String(access.id).toLowerCase() + ":" + String(access.type).toLowerCase()).sort().join(",") === expected; process.exit(matches ? 0 : 1); });' \
    "$GRAPH_APP_ID" "$GRAPH_SCOPE_OPENID" "$GRAPH_SCOPE_PROFILE" "$GRAPH_SCOPE_USER_READ" <<< "$current_json"
}

ensure_required_resource_access() {
  (( $# == 1 )) || die "ensure_required_resource_access: usage ensure_required_resource_access <object-id>"
  local object_id="$1"
  local desired
  desired="$(required_resource_access_json)"
  if is_pending "$object_id"; then
    run az ad app update --id "$object_id" --required-resource-accesses "$desired" --output none --only-show-errors
    return 0
  fi
  local current
  current="$(az_read ad app show --id "$object_id" --query requiredResourceAccess --output json)" || die "cannot read the required resource access of $object_id"
  if required_resource_access_matches "$current"; then
    log_info "Graph delegated permissions unchanged (${GRAPH_SCOPE_VALUES[*]})"
    return 0
  fi
  run az ad app update --id "$object_id" --required-resource-accesses "$desired" --output none --only-show-errors
  log_change "Graph delegated permissions (${GRAPH_SCOPE_VALUES[*]})" updated
}

ensure_service_principal() {
  (( $# == 1 )) || die "ensure_service_principal: usage ensure_service_principal <app-id>"
  local app_id="$1"
  local -a create_command=(az ad sp create --id "$app_id" --query id --output tsv --only-show-errors)
  if is_pending "$app_id"; then
    run "${create_command[@]}"
    printf '%s' "$PENDING_ID"
    return 0
  fi
  local sp_id
  sp_id="$(az_read ad sp show --id "$app_id" --query id --output tsv 2> /dev/null)" || sp_id=''
  if [[ -n "$sp_id" ]]; then
    log_info "service principal unchanged ($sp_id)"
    printf '%s' "$sp_id"
    return 0
  fi
  if (( DRY_RUN )); then
    run "${create_command[@]}"
    log_change "service principal" created
    printf '%s' "$PENDING_ID"
    return 0
  fi
  sp_id="$(retry 6 10 -- az_read ad sp create --id "$app_id" --query id --output tsv)" || die "cannot create the service principal of $app_id"
  is_guid "$sp_id" || die "unexpected response while creating the service principal of $app_id"
  log_info "service principal created ($sp_id)"
  printf '%s' "$sp_id"
}

ensure_assignment_required_once() {
  local sp_id="$1" body="$2"
  local current
  current="$(graph_get "servicePrincipals/$sp_id?\$select=appRoleAssignmentRequired" appRoleAssignmentRequired)" || return 1
  if [[ "${current,,}" == "true" ]]; then
    log_info "assignment required unchanged"
    return 0
  fi
  graph_patch "servicePrincipals/$sp_id" "$body" || return 1
  log_change "assignment required" enabled
}

ensure_assignment_required() {
  (( $# == 1 )) || die "ensure_assignment_required: usage ensure_assignment_required <service-principal-object-id>"
  local sp_id="$1"
  local body='{"appRoleAssignmentRequired":true}'
  if is_pending "$sp_id"; then
    graph_patch "servicePrincipals/$sp_id" "$body"
    return 0
  fi
  retry 6 10 -- ensure_assignment_required_once "$sp_id" "$body"
}

grant_scope_covers() {
  local scope="$1" value
  for value in "${GRAPH_SCOPE_VALUES[@]}"; do
    [[ " $scope " == *" $value "* ]] || return 1
  done
}

permission_grant_scope() {
  local app_id="$1"
  az_read ad app permission list-grants --id "$app_id" --query "[?resourceId=='$GRAPH_SP_OBJECT_ID'].scope | [0]" --output tsv
}

ensure_permission_grant() {
  (( $# == 2 )) || die "ensure_permission_grant: usage ensure_permission_grant <app-id> <service-principal-object-id>"
  local app_id="$1" sp_id="$2"
  local -a grant_command=(az ad app permission grant --id "$app_id" --api "$GRAPH_APP_ID" --scope "${GRAPH_SCOPE_VALUES[*]}"
    --consent-type AllPrincipals --output none --only-show-errors)
  if is_pending "$app_id" || is_pending "$sp_id"; then
    run "${grant_command[@]}"
    return 0
  fi
  local scope
  if ! scope="$(permission_grant_scope "$app_id")"; then
    (( DRY_RUN )) || die "cannot list the permission grants of $app_id"
    log_warn "the permission grants of $app_id could not be read; the dry run assumes none"
    scope=''
  fi
  if grant_scope_covers "$scope"; then
    log_info "Graph consent unchanged ($scope)"
    return 0
  fi
  if run "${grant_command[@]}"; then
    log_change "Graph consent (${GRAPH_SCOPE_VALUES[*]})" granted
    return 0
  fi
  PERMISSION_GRANT_FAILURES+=("$app_id")
  log_warn "Graph refused the consent grant for $app_id (see the error above); when the tenant requires an administrator, run: az ad app permission admin-consent --id $app_id"
}

app_role_assignment_id() {
  local sp_id="$1" principal_id="$2" app_role_id="$3"
  local url="$GRAPH_BASE_URL/servicePrincipals/$sp_id/appRoleAssignedTo?\$top=999"
  local page match
  while [[ -n "$url" ]]; do
    page="$(az_read rest --method get --url "$url" --output json)" || return 1
    match="$(node -e 'let raw = ""; process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => { const page = JSON.parse(raw); const hit = (page.value ?? []).find((row) => row.principalId === process.argv[1] && row.appRoleId === process.argv[2]); process.stdout.write(hit ? hit.id : (page["@odata.nextLink"] ? "next " + page["@odata.nextLink"] : "")); });' "$principal_id" "$app_role_id" <<< "$page")"
    if [[ "$match" == next\ * ]]; then
      url="${match#next }"
      continue
    fi
    printf '%s' "$match"
    return 0
  done
}

ensure_app_role_assignment_once() {
  local sp_id="$1" principal_id="$2" app_role_id="$3" body="$4"
  local existing
  existing="$(app_role_assignment_id "$sp_id" "$principal_id" "$app_role_id")" || return 1
  if [[ -n "$existing" ]]; then
    log_info "app role assignment for $principal_id unchanged"
    return 0
  fi
  graph_post "servicePrincipals/$sp_id/appRoleAssignedTo" "$body" || return 1
  log_change "app role assignment for $principal_id" created
}

ensure_app_role_assignment() {
  (( $# == 3 )) || die "ensure_app_role_assignment: usage ensure_app_role_assignment <service-principal-object-id> <principal-id> <app-role-id>"
  local sp_id="$1" principal_id="$2" app_role_id="$3"
  local body
  body="$(printf '{"principalId":"%s","resourceId":"%s","appRoleId":"%s"}' "$principal_id" "$sp_id" "$app_role_id")"
  if is_pending "$sp_id"; then
    graph_post "servicePrincipals/$sp_id/appRoleAssignedTo" "$body"
    return 0
  fi
  retry 6 10 -- ensure_app_role_assignment_once "$sp_id" "$principal_id" "$app_role_id" "$body"
}

certificate_thumbprint() {
  (( $# == 2 )) || die "certificate_thumbprint: usage certificate_thumbprint <vault> <certificate-name>"
  local vault="$1" name="$2"
  local output
  if output="$(az_read keyvault certificate show --vault-name "$vault" --name "$name" --query x509ThumbprintHex --output tsv 2>&1)"; then
    printf '%s' "$output"
    return 0
  fi
  [[ "$output" == *CertificateNotFound* ]] && return 0
  log_error "cannot read certificate '$name' in vault '$vault': $(az_error_summary "$output")"
  return 1
}

certificate_expiry() {
  local vault="$1" name="$2"
  az_read keyvault certificate show --vault-name "$vault" --name "$name" --query attributes.expires --output tsv
}

remove_certificate_policy_file() {
  [[ -z "$CERTIFICATE_POLICY_FILE" ]] || rm -f -- "$CERTIFICATE_POLICY_FILE"
  CERTIFICATE_POLICY_FILE=''
}

render_certificate_policy() {
  local common_name="$1" content_type="$2"
  require_repo_root_cwd
  [[ -f "$CERTIFICATE_POLICY_TEMPLATE" ]] || die "certificate policy template '$CERTIFICATE_POLICY_TEMPLATE' not found"
  mkdir -p -- "$ENTRA_TEMP_DIR"
  CERTIFICATE_POLICY_FILE="$(mktemp "$ENTRA_TEMP_DIR/certificate-policy.XXXXXX")" || die "cannot create a temporary file under $ENTRA_TEMP_DIR"
  trap remove_certificate_policy_file EXIT
  sed -e "s|__SUBJECT__|$common_name|" -e "s|__CONTENT_TYPE__|$content_type|" "$CERTIFICATE_POLICY_TEMPLATE" > "$CERTIFICATE_POLICY_FILE"
}

create_certificate_version() {
  local vault="$1" name="$2" common_name="$3" content_type="$4"
  if (( DRY_RUN )); then
    run az keyvault certificate create --vault-name "$vault" --name "$name" --policy "@$CERTIFICATE_POLICY_TEMPLATE" --output none --only-show-errors
    log_info "the policy is rendered with CN=$common_name and content type $content_type before the call"
    printf '%s' "$PENDING_ID"
    return 0
  fi
  render_certificate_policy "$common_name" "$content_type"
  retry -- az keyvault certificate create --vault-name "$vault" --name "$name" --policy "@$CERTIFICATE_POLICY_FILE" --output none --only-show-errors
  remove_certificate_policy_file
  local thumbprint
  thumbprint="$(certificate_thumbprint "$vault" "$name")" || die "certificate '$name' in vault '$vault' cannot be read after creation"
  [[ -n "$thumbprint" ]] || die "certificate '$name' in vault '$vault' is missing after creation"
  printf '%s' "$thumbprint"
}

ensure_certificate() {
  (( $# == 4 )) || die "ensure_certificate: usage ensure_certificate <vault> <certificate-name> <common-name> <content-type>"
  local vault="$1" name="$2" common_name="$3" content_type="$4"
  local thumbprint
  if ! thumbprint="$(certificate_thumbprint "$vault" "$name")"; then
    (( DRY_RUN )) || die "cannot check certificate '$name' in vault '$vault'"
    log_warn "vault '$vault' could not be read; the dry run assumes certificate '$name' is absent"
    thumbprint=''
  fi
  if [[ -n "$thumbprint" ]]; then
    log_info "certificate '$name' in $vault unchanged (thumbprint $thumbprint)"
    printf '%s' "$thumbprint"
    return 0
  fi
  thumbprint="$(create_certificate_version "$vault" "$name" "$common_name" "$content_type")" || die "cannot create certificate '$name' in vault '$vault'"
  if is_pending "$thumbprint"; then
    log_change "certificate '$name' in $vault" created " (CN=$common_name, $content_type)"
  else
    log_info "certificate '$name' in $vault created (thumbprint $thumbprint)"
  fi
  printf '%s' "$thumbprint"
}

rotate_certificate() {
  (( $# == 4 )) || die "rotate_certificate: usage rotate_certificate <vault> <certificate-name> <common-name> <content-type>"
  local vault="$1" name="$2" common_name="$3" content_type="$4"
  local current
  current="$(certificate_thumbprint "$vault" "$name")" || die "cannot check certificate '$name' in vault '$vault'"
  [[ -n "$current" ]] || die "certificate '$name' does not exist in vault '$vault'; run: bash scripts/azure/entra.sh"
  local thumbprint
  thumbprint="$(create_certificate_version "$vault" "$name" "$common_name" "$content_type")" || die "cannot rotate certificate '$name' in vault '$vault'"
  if is_pending "$thumbprint"; then
    log_change "certificate '$name' in $vault" rotated " (current thumbprint $current)"
  else
    log_info "certificate '$name' in $vault rotated (thumbprint $current -> $thumbprint)"
  fi
  printf '%s' "$thumbprint"
}

key_credential_thumbprints() {
  local app_id="$1"
  local rows
  rows="$(graph_get "applications(appId='$app_id')?\$select=keyCredentials" 'keyCredentials[].[keyId, customKeyIdentifier]')" || return 1
  [[ -n "$rows" ]] || return 0
  local key_id identifier
  while IFS=$'\t' read -r key_id identifier; do
    [[ -n "$key_id" ]] || continue
    printf '%s\t%s\n' "$key_id" "$(base64_to_hex "$identifier")"
  done <<< "$rows"
}

key_credential_id_for_thumbprint() {
  local app_id="$1" thumbprint="$2"
  local rows
  rows="$(key_credential_thumbprints "$app_id")" || return 1
  local key_id candidate
  while IFS=$'\t' read -r key_id candidate; do
    [[ -n "$key_id" ]] || continue
    if [[ "${candidate,,}" == "${thumbprint,,}" ]]; then
      printf '%s' "$key_id"
      return 0
    fi
  done <<< "$rows"
}

ensure_key_credential() {
  (( $# == 4 )) || die "ensure_key_credential: usage ensure_key_credential <app-id> <vault> <certificate-name> <thumbprint-hex>"
  local app_id="$1" vault="$2" name="$3" thumbprint="$4"
  local -a reset_command=(az ad app credential reset --id "$app_id" --keyvault "$vault" --cert "$name" --append --output none --only-show-errors)
  if is_pending "$app_id" || is_pending "$thumbprint"; then
    run "${reset_command[@]}"
    return 0
  fi
  [[ -n "$thumbprint" ]] || die "ensure_key_credential: no certificate thumbprint for '$name' in vault '$vault'"
  local key_id
  key_id="$(key_credential_id_for_thumbprint "$app_id" "$thumbprint")" || die "cannot read the key credentials of $app_id"
  if [[ -n "$key_id" ]]; then
    log_info "key credential unchanged (key id $key_id, thumbprint $thumbprint)"
    return 0
  fi
  run "${reset_command[@]}"
  if (( DRY_RUN )); then
    log_change "key credential for thumbprint $thumbprint" added
    return 0
  fi
  key_id="$(key_credential_id_for_thumbprint "$app_id" "$thumbprint")" || die "cannot read the key credentials of $app_id"
  [[ -n "$key_id" ]] || die "the key credential for thumbprint $thumbprint is not visible on $app_id after the upload; re-run to check"
  log_info "key credential added (key id $key_id, thumbprint $thumbprint)"
}

prune_key_credentials() {
  (( $# == 2 )) || die "prune_key_credentials: usage prune_key_credentials <app-id> <keep-thumbprint-hex>"
  local app_id="$1" keep_thumbprint="$2"
  local rows
  rows="$(key_credential_thumbprints "$app_id")" || die "cannot read the key credentials of $app_id"
  local keep_found=0 key_id thumbprint
  local -a stale_ids=()
  while IFS=$'\t' read -r key_id thumbprint; do
    [[ -n "$key_id" ]] || continue
    if [[ "${thumbprint,,}" == "${keep_thumbprint,,}" ]]; then
      keep_found=1
    else
      stale_ids+=("$key_id")
    fi
  done <<< "$rows"
  (( keep_found )) || die "no key credential of $app_id matches the current certificate thumbprint $keep_thumbprint; run entra.sh (or its --rotate flag) first so the current certificate is registered before pruning"
  if (( ${#stale_ids[@]} == 0 )); then
    log_info "key credentials unchanged (only the current certificate is registered)"
    return 0
  fi
  for key_id in "${stale_ids[@]}"; do
    run az ad app credential delete --id "$app_id" --key-id "$key_id" --cert --output none --only-show-errors
    log_change "key credential $key_id" deleted
  done
}
