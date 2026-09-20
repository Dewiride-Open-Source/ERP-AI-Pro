#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/verify.sh [--entra] [--params <file>]

Reads the provisioned Azure resources back and asserts the documented state:
the App Configuration store, both Key Vaults, the data-protection keys, every
row of the role matrix and the labelled Erp:Sentinel keys. Never writes.

Options:
  --entra           Check the Entra app registrations, service principals,
                    certificates, role assignments and identity keys written
                    by entra.sh instead of the provisioned resources.
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/appconfig.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/appconfig.sh"
# shellcheck source=lib/graph.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/graph.sh"

readonly SENTINEL_KEY='Erp:Sentinel'
readonly LABELS=(local-dev production)
readonly DATA_PROTECTION_KEY_NAME='Erp--Platform--DataProtection--Key'

readonly ROLE_APP_CONFIGURATION_DATA_READER='516239f1-63e1-4d78-a4de-a74fb236a071'
readonly ROLE_APP_CONFIGURATION_DATA_OWNER='5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b'
readonly ROLE_KEY_VAULT_SECRETS_USER='4633458b-17de-408a-b874-0445c86b69e6'
readonly ROLE_KEY_VAULT_SECRETS_OFFICER='b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
readonly ROLE_KEY_VAULT_CRYPTO_USER='12338af0-0e69-4776-bea7-57ae8d297424'
readonly ROLE_KEY_VAULT_CRYPTO_OFFICER='14b46e9e-c2b7-41b4-b07b-48a6ebf60603'
readonly ROLE_KEY_VAULT_CERTIFICATES_OFFICER='a4417e6f-fecd-4de8-b567-7b0420556985'

readonly ENTRA_HINT='run: bash scripts/azure/entra.sh'
readonly PROVISION_HINT='run: bash scripts/azure/provision.sh'

ENTRA_MODE=0
FAILURES=0
OPERATOR_ID=''

pass() { printf '[ok]   %s\n' "$*"; }

fail() {
  printf '[FAIL] %s\n' "$*"
  FAILURES=$((FAILURES + 1))
}

skip() { printf '[skip] %s\n' "$*"; }

warn() { printf '[warn] %s\n' "$*"; }

query_error_summary() {
  printf '%s' "$1" | tr -d '\r' | sed -n '/[^[:space:]]/p' | tail -n 1
}

assert_equals() {
  local description="$1" expected="$2" actual="$3"
  if [[ "${actual,,}" == "${expected,,}" ]]; then
    pass "$description"
  else
    fail "$description (expected '$expected', got '${actual:-<none>}')"
  fi
}

assert_query() {
  local description="$1" expected="$2"
  shift 2
  local output
  if output="$(az_read "$@" --output tsv 2>&1)"; then
    assert_equals "$description" "$expected" "$output"
  else
    fail "$description (query failed: $(query_error_summary "$output"))"
  fi
}

assert_role() {
  local description="$1" scope="$2" principal_id="$3" role_id="$4"
  assert_query "$description" '1' role assignment list --scope "$scope" --assignee-object-id "$principal_id" --role "$role_id" \
    --fill-principal-name false --query 'length(@)'
}

json_eval() {
  local json="$1" expression="$2"
  shift 2
  node -e 'let raw = ""; process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => { const value = JSON.parse(raw); const result = new Function("value", "args", "return (" + process.argv[1] + ");")(value, process.argv.slice(2)); process.stdout.write(result == null ? "" : Array.isArray(result) ? result.join("\n") : String(result)); });' "$expression" "$@" <<< "$json"
}

seconds_until() {
  node -e 'process.stdout.write(String(Math.floor((Date.parse(process.argv[1]) - Date.now()) / 1000)));' "$1"
}

verify_store() {
  log_step "App Configuration store $ERP_AZURE_APPCONFIG_NAME"
  local store_json
  if ! store_json="$(az_read appconfig show --name "$ERP_AZURE_APPCONFIG_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" --output json 2>&1)"; then
    fail "store exists in resource group $ERP_AZURE_RESOURCE_GROUP ($(query_error_summary "$store_json"))"
    return 0
  fi
  pass "store exists in resource group $ERP_AZURE_RESOURCE_GROUP"

  local endpoint sku disable_local_auth purge_protection
  endpoint="$(json_field "$store_json" endpoint)"
  sku="$(json_field "$store_json" sku.name)"
  disable_local_auth="$(json_field "$store_json" disableLocalAuth)"
  purge_protection="$(json_field "$store_json" enablePurgeProtection)"

  assert_equals "store local authentication disabled" 'true' "$disable_local_auth"
  assert_equals "store sku is $ERP_AZURE_APPCONFIG_SKU" "$ERP_AZURE_APPCONFIG_SKU" "$sku"
  case "$ERP_AZURE_APPCONFIG_SKU" in
    Standard | Premium) assert_equals "store purge protection enabled" 'true' "$purge_protection" ;;
    *) skip "store purge protection: not offered by the $ERP_AZURE_APPCONFIG_SKU tier" ;;
  esac
  if [[ -n "$endpoint" ]]; then
    pass "store endpoint $endpoint"
  else
    fail "store endpoint missing"
  fi
}

verify_vault() {
  local vault_name="$1"
  log_step "Key Vault $vault_name"
  assert_query "$vault_name uses RBAC authorization" 'true' keyvault show --name "$vault_name" --query properties.enableRbacAuthorization
  assert_query "$vault_name soft delete enabled" 'true' keyvault show --name "$vault_name" --query properties.enableSoftDelete
  assert_query "$vault_name purge protection enabled" 'true' keyvault show --name "$vault_name" --query properties.enablePurgeProtection
  assert_query "$vault_name holds RSA key $DATA_PROTECTION_KEY_NAME" 'RSA' keyvault key show --vault-name "$vault_name" --name "$DATA_PROTECTION_KEY_NAME" --query key.kty
}

verify_roles() {
  local operator_id="$1" developers_group_id="$2" runtime_id="$3"
  local store_id development_vault_id production_vault_id
  store_id="$(store_resource_id)"
  development_vault_id="$(vault_resource_id "$ERP_AZURE_KEYVAULT_DEV_NAME")"
  production_vault_id="$(vault_resource_id "$ERP_AZURE_KEYVAULT_PROD_NAME")"

  log_step "Role assignments: operator"
  assert_role "operator is App Configuration Data Owner on the store" "$store_id" "$operator_id" "$ROLE_APP_CONFIGURATION_DATA_OWNER"
  local vault_id vault_name
  for vault_name in "$ERP_AZURE_KEYVAULT_DEV_NAME" "$ERP_AZURE_KEYVAULT_PROD_NAME"; do
    vault_id="$(vault_resource_id "$vault_name")"
    assert_role "operator is Key Vault Secrets Officer on $vault_name" "$vault_id" "$operator_id" "$ROLE_KEY_VAULT_SECRETS_OFFICER"
    assert_role "operator is Key Vault Certificates Officer on $vault_name" "$vault_id" "$operator_id" "$ROLE_KEY_VAULT_CERTIFICATES_OFFICER"
    assert_role "operator is Key Vault Crypto Officer on $vault_name" "$vault_id" "$operator_id" "$ROLE_KEY_VAULT_CRYPTO_OFFICER"
  done

  log_step "Role assignments: developers group"
  if [[ -n "$developers_group_id" ]]; then
    assert_role "developers group is App Configuration Data Reader on the store" "$store_id" "$developers_group_id" "$ROLE_APP_CONFIGURATION_DATA_READER"
    assert_role "developers group is Key Vault Secrets User on $ERP_AZURE_KEYVAULT_DEV_NAME" "$development_vault_id" "$developers_group_id" "$ROLE_KEY_VAULT_SECRETS_USER"
    assert_role "developers group is Key Vault Crypto User on $ERP_AZURE_KEYVAULT_DEV_NAME" "$development_vault_id" "$developers_group_id" "$ROLE_KEY_VAULT_CRYPTO_USER"
  else
    skip "developers group roles: skipped: not configured"
  fi

  log_step "Role assignments: runtime service principal"
  if [[ -n "$runtime_id" ]]; then
    assert_role "runtime service principal is App Configuration Data Reader on the store" "$store_id" "$runtime_id" "$ROLE_APP_CONFIGURATION_DATA_READER"
    assert_role "runtime service principal is Key Vault Secrets User on $ERP_AZURE_KEYVAULT_PROD_NAME" "$production_vault_id" "$runtime_id" "$ROLE_KEY_VAULT_SECRETS_USER"
    assert_role "runtime service principal is Key Vault Crypto User on $ERP_AZURE_KEYVAULT_PROD_NAME" "$production_vault_id" "$runtime_id" "$ROLE_KEY_VAULT_CRYPTO_USER"
  else
    skip "runtime service principal roles: skipped: not configured"
  fi
}

verify_sentinels() {
  log_step "Sentinel keys"
  local label value
  for label in "${LABELS[@]}"; do
    if value="$(appconfig_kv_get "$SENTINEL_KEY" "$label" 2>&1)"; then
      if [[ -n "$value" ]]; then
        pass "$SENTINEL_KEY [$label] present ($value)"
      else
        fail "$SENTINEL_KEY [$label] present (no value)"
      fi
    else
      fail "$SENTINEL_KEY [$label] present (query failed: $(query_error_summary "$value"))"
    fi
  done
}

load_registration() {
  local display_name="$1"
  local -n registration="$2"
  local list_json count
  registration=''
  if ! list_json="$(az_read ad app list --filter "displayName eq $(odata_string_literal "$display_name")" --output json 2>&1)"; then
    fail "registration '$display_name' exists (query failed: $(query_error_summary "$list_json"))"
    return 1
  fi
  count="$(json_eval "$list_json" 'value.length')"
  case "$count" in
    0)
      fail "registration '$display_name' exists; $ENTRA_HINT"
      return 1
      ;;
    1)
      registration="$(json_eval "$list_json" 'JSON.stringify(value[0])')"
      pass "registration '$display_name' exists ($(json_eval "$registration" 'value.appId'))"
      ;;
    *)
      fail "registration '$display_name' is unique ($count registrations share the name: $(json_eval "$list_json" 'value.map((app) => app.appId).join(", ")'))"
      return 1
      ;;
  esac
}

load_service_principal() {
  local app_id="$1" display_name="$2"
  local -n principal="$3"
  if ! principal="$(az_read ad sp show --id "$app_id" --output json 2>&1)"; then
    principal=''
    fail "service principal for '$display_name' exists; $ENTRA_HINT"
    return 1
  fi
  pass "service principal for '$display_name' exists ($(json_eval "$principal" 'value.id'))"
}

verify_registration_shape() {
  local registration_json="$1" display_name="$2"
  assert_equals "'$display_name' is single tenant" 'AzureADMyOrg' "$(json_eval "$registration_json" 'value.signInAudience')"
  assert_equals "'$display_name' has no client secret" '0' "$(json_eval "$registration_json" '(value.passwordCredentials || []).length')"
  assert_equals "'$display_name' has no single-page, mobile or fallback public client platform" '0' \
    "$(json_eval "$registration_json" '((value.spa && value.spa.redirectUris) || []).length + ((value.publicClient && value.publicClient.redirectUris) || []).length + (value.isFallbackPublicClient === true ? 1 : 0)')"
  local owners
  if owners="$(app_owner_ids "$(json_eval "$registration_json" 'value.id')" 2>&1)"; then
    if grep -qix "$OPERATOR_ID" <<< "$owners"; then
      pass "operator owns '$display_name'"
    else
      fail "operator owns '$display_name' (not in the owner list; the registration was not created by these scripts)"
    fi
  else
    fail "operator owns '$display_name' (query failed: $(query_error_summary "$owners"))"
  fi
}

verify_service_principal_shape() {
  local principal_json="$1" display_name="$2"
  assert_equals "'$display_name' service principal has no client secret" '0' "$(json_eval "$principal_json" '(value.passwordCredentials || []).length')"
}

verify_signin_registration_shape() {
  local registration_json="$1" display_name="$2" label="$3"
  local expected_uris actual_uris
  expected_uris="$(signin_redirect_uris "$label" | LC_ALL=C sort | tr '\n' ' ')"
  expected_uris="${expected_uris% }"
  actual_uris="$(json_eval "$registration_json" '((value.web && value.web.redirectUris) || []).slice().sort().join(" ")')"
  assert_equals "'$display_name' redirect URIs are [$expected_uris]" "$expected_uris" "$actual_uris"
  if [[ "$label" == production && -z "$ERP_AZURE_PRODUCTION_WEB_ORIGIN" ]]; then
    warn "'$display_name' has no redirect URI: production sign-in stays impossible until ERP_AZURE_PRODUCTION_WEB_ORIGIN is set and entra.sh re-run"
  fi
  assert_equals "'$display_name' implicit id token issuance off" 'false' \
    "$(json_eval "$registration_json" 'value.web && value.web.implicitGrantSettings && value.web.implicitGrantSettings.enableIdTokenIssuance')"
  assert_equals "'$display_name' implicit access token issuance off" 'false' \
    "$(json_eval "$registration_json" 'value.web && value.web.implicitGrantSettings && value.web.implicitGrantSettings.enableAccessTokenIssuance')"
  assert_equals "'$display_name' requested access token version is 2" '2' \
    "$(json_eval "$registration_json" 'value.api && value.api.requestedAccessTokenVersion')"
}

verify_app_roles() {
  local registration_json="$1" display_name="$2"
  local role_id role_value
  for role_value in "$APP_ROLE_USER_VALUE" "$APP_ROLE_ADMIN_VALUE"; do
    role_id="$(app_role_id "$role_value")"
    assert_equals "'$display_name' app role $role_id is $role_value and enabled" "$role_value enabled" \
      "$(json_eval "$registration_json" '(value.appRoles || []).filter((r) => String(r.id).toLowerCase() === args[0].toLowerCase()).map((r) => r.value + (r.isEnabled ? " enabled" : " disabled"))[0]' "$role_id")"
  done
}

verify_required_resource_access() {
  local registration_json="$1" display_name="$2"
  local requested_ids index scope scope_id
  local -a scope_ids=("$GRAPH_SCOPE_OPENID" "$GRAPH_SCOPE_PROFILE" "$GRAPH_SCOPE_USER_READ")
  requested_ids="$(json_eval "$registration_json" '(value.requiredResourceAccess || []).filter((r) => String(r.resourceAppId).toLowerCase() === args[0].toLowerCase()).flatMap((r) => r.resourceAccess || []).filter((a) => a.type === "Scope").map((a) => String(a.id).toLowerCase())' "$GRAPH_APP_ID")"
  for index in "${!GRAPH_SCOPE_VALUES[@]}"; do
    scope="${GRAPH_SCOPE_VALUES[$index]}"
    scope_id="${scope_ids[$index]}"
    if grep -qix "$scope_id" <<< "$requested_ids"; then
      pass "'$display_name' requests Microsoft Graph delegated scope $scope"
    else
      fail "'$display_name' requests Microsoft Graph delegated scope $scope ($scope_id not in requiredResourceAccess); $ENTRA_HINT"
    fi
  done
}

verify_permission_grant() {
  local app_id="$1" display_name="$2"
  local granted scope
  if ! granted="$(permission_grant_scope "$app_id" 2>&1)"; then
    fail "'$display_name' Microsoft Graph consent granted (query failed: $(query_error_summary "$granted"))"
    return 0
  fi
  for scope in "${GRAPH_SCOPE_VALUES[@]}"; do
    if [[ " $granted " == *" $scope "* ]]; then
      pass "'$display_name' Microsoft Graph consent covers $scope"
    else
      fail "'$display_name' Microsoft Graph consent covers $scope (granted: '${granted:-<none>}'); $ENTRA_HINT or, as Global Administrator: az ad app permission admin-consent --id $app_id"
    fi
  done
  if grant_scope_matches "$granted"; then
    pass "'$display_name' tenant-wide Microsoft Graph consent is exactly ${GRAPH_SCOPE_VALUES[*]}"
  else
    fail "'$display_name' tenant-wide Microsoft Graph consent is exactly ${GRAPH_SCOPE_VALUES[*]} (granted: '${granted:-<none>}'); $ENTRA_HINT"
  fi
}

verify_app_role_assignment() {
  local service_principal_id="$1" principal_id="$2" display_name="$3"
  local admin_role_id path page count=0 page_count
  admin_role_id="$(app_role_id "$APP_ROLE_ADMIN_VALUE")"
  path="servicePrincipals/$service_principal_id/appRoleAssignedTo"
  while [[ -n "$path" ]]; do
    if ! page="$(graph_get "$path" 2>&1)"; then
      fail "operator holds $APP_ROLE_ADMIN_VALUE on '$display_name' (query failed: $(query_error_summary "$page"))"
      return 0
    fi
    page_count="$(json_eval "$page" '(value.value || []).filter((a) => String(a.principalId).toLowerCase() === args[0].toLowerCase() && String(a.appRoleId).toLowerCase() === args[1].toLowerCase()).length' "$principal_id" "$admin_role_id")"
    count=$((count + page_count))
    path="$(json_eval "$page" 'typeof value["@odata.nextLink"] === "string" && value["@odata.nextLink"].startsWith(args[0]) ? value["@odata.nextLink"].slice(args[0].length) : ""' "$GRAPH_BASE_URL/")"
  done
  if (( count > 0 )); then
    pass "operator holds $APP_ROLE_ADMIN_VALUE on '$display_name'"
  else
    fail "operator holds $APP_ROLE_ADMIN_VALUE on '$display_name'; $ENTRA_HINT"
  fi
}

verify_certificate_expiry() {
  local vault_name="$1" certificate_name="$2" rotate_hint="$3"
  local expires remaining_seconds remaining_days
  if ! expires="$(certificate_expiry "$vault_name" "$certificate_name" 2>&1)"; then
    fail "certificate $certificate_name in $vault_name has an expiry date (query failed: $(query_error_summary "$expires"))"
    return 0
  fi
  remaining_seconds="$(seconds_until "$expires")"
  if [[ ! "$remaining_seconds" =~ ^-?[0-9]+$ ]]; then
    fail "certificate $certificate_name in $vault_name has a readable expiry date (got '${expires:-<none>}')"
    return 0
  fi
  remaining_days=$(( remaining_seconds / 86400 ))
  if (( remaining_seconds < 0 )); then
    fail "certificate $certificate_name in $vault_name is valid (expired on $expires); $rotate_hint"
  elif (( remaining_seconds <= CERTIFICATE_EXPIRY_WARNING_SECONDS )); then
    warn "certificate $certificate_name in $vault_name expires in $remaining_days day(s) on $expires; $rotate_hint"
  else
    pass "certificate $certificate_name in $vault_name is valid for $remaining_days more day(s)"
  fi
}

verify_certificate_credential() {
  local app_id="$1" display_name="$2" vault_name="$3" certificate_name="$4" rotate_hint="$5"
  local thumbprint credentials thumbprints
  if ! thumbprint="$(certificate_thumbprint "$vault_name" "$certificate_name" 2>&1)"; then
    fail "certificate $certificate_name exists in $vault_name ($(query_error_summary "$thumbprint"))"
    return 0
  fi
  if [[ -z "$thumbprint" ]]; then
    fail "certificate $certificate_name exists in $vault_name; $ENTRA_HINT"
    return 0
  fi
  pass "certificate $certificate_name exists in $vault_name (thumbprint $thumbprint)"

  verify_certificate_expiry "$vault_name" "$certificate_name" "$rotate_hint"

  if ! credentials="$(key_credential_thumbprints "$app_id" 2>&1)"; then
    fail "'$display_name' holds the key credential for $certificate_name (query failed: $(query_error_summary "$credentials"))"
    return 0
  fi
  thumbprints="$(printf '%s\n' "$credentials" | awk -F '\t' '{ print $2 }')"
  if grep -qix "$thumbprint" <<< "$thumbprints"; then
    pass "'$display_name' holds the key credential for $certificate_name"
  else
    fail "'$display_name' holds the key credential for $certificate_name (no key credential matches thumbprint $thumbprint); $ENTRA_HINT"
  fi
}

assert_appconfig_value() {
  local description="$1" key="$2" label="$3" expected="$4"
  local value
  if value="$(appconfig_kv_get "$key" "$label" 2>&1)"; then
    assert_equals "$description" "$expected" "$value"
  else
    fail "$description (query failed: $(query_error_summary "$value"))"
  fi
}

verify_signin_registration() {
  local label="$1" operator_id="$2"
  local display_name vault_name
  display_name="$(signin_app_name "$label")"
  vault_name="$(signin_vault "$label")"
  log_step "Sign-in registration '$display_name' [$label]"
  local registration_json app_id principal_json principal_id
  load_registration "$display_name" registration_json || return 0
  app_id="$(json_eval "$registration_json" 'value.appId')"

  verify_registration_shape "$registration_json" "$display_name"
  verify_signin_registration_shape "$registration_json" "$display_name" "$label"
  verify_app_roles "$registration_json" "$display_name"
  verify_required_resource_access "$registration_json" "$display_name"

  if load_service_principal "$app_id" "$display_name" principal_json; then
    principal_id="$(json_eval "$principal_json" 'value.id')"
    verify_service_principal_shape "$principal_json" "$display_name"
    assert_equals "'$display_name' service principal requires assignment" 'true' "$(json_eval "$principal_json" 'value.appRoleAssignmentRequired')"
    verify_permission_grant "$app_id" "$display_name"
    verify_app_role_assignment "$principal_id" "$operator_id" "$display_name"
  fi

  verify_certificate_credential "$app_id" "$display_name" "$vault_name" "$SIGNIN_CERTIFICATE_NAME" \
    "rotate with: bash scripts/azure/entra.sh --rotate-signin-certificate $label"

  assert_appconfig_value "$IDENTITY_TENANT_ID_KEY [$label] is the tenant id" "$IDENTITY_TENANT_ID_KEY" "$label" "$ERP_AZURE_TENANT_ID"
  assert_appconfig_value "$IDENTITY_CLIENT_ID_KEY [$label] is the application id of '$display_name'" "$IDENTITY_CLIENT_ID_KEY" "$label" "$app_id"
}

assert_runtime_role() {
  local description="$1" scope="$2" principal_id="$3" role_id="$4"
  local output
  if ! output="$(az_read role assignment list --scope "$scope" --assignee-object-id "$principal_id" --role "$role_id" \
    --fill-principal-name false --query 'length(@)' --output tsv 2>&1)"; then
    fail "$description (query failed: $(query_error_summary "$output"))"
  elif [[ "$output" == '1' ]]; then
    pass "$description"
  else
    fail "$description (assignment missing); $PROVISION_HINT again"
  fi
}

verify_runtime_registration() {
  local display_name="$ERP_AZURE_APP_RUNTIME_NAME"
  log_step "Runtime registration '$display_name'"
  local registration_json app_id principal_json principal_id
  load_registration "$display_name" registration_json || return 0
  app_id="$(json_eval "$registration_json" 'value.appId')"

  verify_registration_shape "$registration_json" "$display_name"
  assert_equals "'$display_name' has no redirect URI" '0' "$(json_eval "$registration_json" '((value.web && value.web.redirectUris) || []).length')"

  verify_certificate_credential "$app_id" "$display_name" "$ERP_AZURE_KEYVAULT_PROD_NAME" "$RUNTIME_CERTIFICATE_NAME" \
    "rotate with: bash scripts/azure/entra.sh --rotate-runtime-certificate"

  load_service_principal "$app_id" "$display_name" principal_json || return 0
  principal_id="$(json_eval "$principal_json" 'value.id')"
  verify_service_principal_shape "$principal_json" "$display_name"
  local store_id production_vault_id
  store_id="$(store_resource_id)"
  production_vault_id="$(vault_resource_id "$ERP_AZURE_KEYVAULT_PROD_NAME")"
  assert_runtime_role "runtime service principal is App Configuration Data Reader on the store" "$store_id" "$principal_id" "$ROLE_APP_CONFIGURATION_DATA_READER"
  assert_runtime_role "runtime service principal is Key Vault Secrets User on $ERP_AZURE_KEYVAULT_PROD_NAME" "$production_vault_id" "$principal_id" "$ROLE_KEY_VAULT_SECRETS_USER"
  assert_runtime_role "runtime service principal is Key Vault Crypto User on $ERP_AZURE_KEYVAULT_PROD_NAME" "$production_vault_id" "$principal_id" "$ROLE_KEY_VAULT_CRYPTO_USER"
}

verify_entra() {
  local operator_id="$1"
  OPERATOR_ID="$operator_id"
  log_step "Resolving Microsoft Graph"
  resolve_graph_scope_ids
  log_info "Microsoft Graph service principal: $GRAPH_SP_OBJECT_ID"
  local label
  for label in "${SIGNIN_LABELS[@]}"; do
    verify_signin_registration "$label" "$operator_id"
  done
  verify_runtime_registration
}

verify_provisioning() {
  local operator_id="$1"
  local developers_group_id runtime_id
  developers_group_id="$(developers_group_object_id)"
  runtime_id="$(runtime_service_principal_object_id)"

  verify_store
  verify_vault "$ERP_AZURE_KEYVAULT_DEV_NAME"
  verify_vault "$ERP_AZURE_KEYVAULT_PROD_NAME"
  verify_roles "$operator_id" "$developers_group_id" "$runtime_id"
  verify_sentinels
}

main() {
  local -a arguments=()
  local argument
  for argument in "$@"; do
    if [[ "$argument" == '--entra' ]]; then
      ENTRA_MODE=1
    else
      arguments+=("$argument")
    fi
  done
  parse_args "${arguments[@]}"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az node base64 od

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  log_step "Resolving principals"
  local operator_id
  operator_id="$(signed_in_user_object_id)"

  if (( ENTRA_MODE )); then
    verify_entra "$operator_id"
  else
    verify_provisioning "$operator_id"
  fi

  log_step "Result"
  if (( FAILURES > 0 )); then
    printf '[FAIL] %d check(s) failed\n' "$FAILURES"
    exit 1
  fi
  printf '[ok]   all checks passed\n'
}

main "$@"
