#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/verify.sh [--entra] [--labels] [--database] [--params <file>]

Reads the provisioned Azure resources back and asserts the documented state:
the App Configuration store, the three Key Vaults, the data-protection keys,
the development storage account (properties, blob service, private attachments
container, delete lock and a read-only blob listing signed in with Entra ID),
the production storage account (the same properties, geo-redundant, 90 days of
soft delete, reachable only from the address of the database'"'"'s rule home, with
no IPv6 rule, open private endpoint connection or network security perimeter,
and no role on it or in it but the runtime service principal'"'"'s), Shared Key and
data sharing off on both accounts, every row of the role matrix, the runtime and
migrator service principals holding exactly their roles (the migrator only the
production database'"'"'s two connection strings), the operations vault open to the
operator alone, the absence of Key Vault Crypto User for the developers group,
and the labelled Erp:Sentinel keys. Never writes.

Options:
  --database        Check the production database instead: the server
                    (Microsoft Entra-only, the operator as its Microsoft Entra
                    admin, TLS 1.2, IPv6 off, no virtual network rule or
                    private endpoint, delete lock), its one firewall rule, the
                    Basic database and its 7-day restore, the two
                    connection-string secrets, and, through the Go sqlcmd as
                    the Microsoft Entra admin, the users and rights
                    database.sh converges, with nothing left for it to change.
  --entra           Check the Entra app registrations (with their optional
                    claims and front-channel logout URLs), service principals,
                    certificates (each registration signing in with its
                    current certificate alone, the client certificates of the
                    runtime and the migrator only in the operations vault),
                    role assignments and identity keys written by entra.sh
                    instead of the provisioned resources.
  --labels          Check the seeded settings, feature flags and Key Vault
                    references written by seed.sh: every value of
                    infra/appconfig is read back (a reference only under the
                    labels it lists), the local-dev label is proven to
                    override the unlabelled default, the blob endpoints
                    provision.sh writes under local-dev and production are
                    compared with their storage accounts, and the
                    data-protection key identifier it writes under local-dev
                    and production is compared with the key in the matching
                    vault.
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/appconfig.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/appconfig.sh"
# shellcheck source=lib/graph.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/graph.sh"
# shellcheck source=lib/sql.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/sql.sh"
# shellcheck source=lib/storage.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/storage.sh"

readonly SENTINEL_KEY='Erp:Sentinel'
readonly LABELS=(local-dev production)
readonly DATA_PROTECTION_KEY_NAME='Erp--Platform--DataProtection--Key'

readonly ROLE_APP_CONFIGURATION_DATA_READER='516239f1-63e1-4d78-a4de-a74fb236a071'
readonly ROLE_APP_CONFIGURATION_DATA_OWNER='5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b'
readonly ROLE_KEY_VAULT_SECRETS_USER='4633458b-17de-408a-b874-0445c86b69e6'
readonly ROLE_KEY_VAULT_SECRETS_OFFICER='b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
readonly ROLE_KEY_VAULT_CRYPTO_USER='12338af0-0e69-4776-bea7-57ae8d297424'
readonly ROLE_KEY_VAULT_CRYPTO_SERVICE_ENCRYPTION_USER='e147488a-f6f5-4113-8e2d-b22465e65bf6'
readonly ROLE_KEY_VAULT_CRYPTO_OFFICER='14b46e9e-c2b7-41b4-b07b-48a6ebf60603'
readonly ROLE_KEY_VAULT_CERTIFICATES_OFFICER='a4417e6f-fecd-4de8-b567-7b0420556985'
readonly ROLE_STORAGE_BLOB_DATA_CONTRIBUTOR='ba92f5b4-2d11-453d-a403-e96b0029c9fe'

readonly ATTACHMENTS_CONTAINER_NAME='attachments'
readonly DEVELOPMENT_STORAGE_SKU='Standard_LRS'
readonly DEVELOPMENT_STORAGE_RETENTION_DAYS='7'
readonly PRODUCTION_STORAGE_SKU='Standard_GRS'
readonly PRODUCTION_STORAGE_RETENTION_DAYS='90'
readonly STORAGE_LOCK_NAME='do-not-delete'
readonly BLOB_SERVICE_URI_KEY='Erp:Platform:Attachments:BlobServiceUri'
readonly DATA_PROTECTION_KEY_IDENTIFIER_KEY='Erp:Platform:DataProtection:KeyIdentifier'

readonly SQL_DATABASE_SKU='Basic'
readonly SQL_DATABASE_CAPACITY='5'
readonly SQL_DATABASE_MAX_SIZE_BYTES='2147483648'
readonly SQL_BACKUP_STORAGE_REDUNDANCY='Geo'
readonly SQL_RESTORE_DAYS='7'

readonly ENTRA_HINT='run: bash scripts/azure/entra.sh'
readonly PROVISION_HINT='run: bash scripts/azure/provision.sh'
readonly SEED_HINT='run: bash scripts/azure/seed.sh'
readonly DATABASE_HINT='run: bash scripts/azure/database.sh'
readonly SEED_DIRECTORY='infra/appconfig'
readonly SEED_FILES_CLI='scripts/azure/lib/seed-files.ts'
readonly LABEL_PRECEDENCE_KEY='Erp:Platform:Host:ApplicationName'

ENTRA_MODE=0
LABELS_MODE=0
DATABASE_MODE=0
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
  local description="$1" expected="$2" actual="$3" hint="${4:-}"
  if [[ "${actual,,}" == "${expected,,}" ]]; then
    pass "$description"
  else
    fail "$description (expected '$expected', got '${actual:-<none>}')${hint:+; $hint}"
  fi
}

assert_equals_exact() {
  local description="$1" expected="$2" actual="$3"
  if [[ "$actual" == "$expected" ]]; then
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

assert_no_role() {
  local description="$1" scope="$2" principal_id="$3" role_id="$4"
  local output
  if ! output="$(az_read role assignment list --scope "$scope" --assignee-object-id "$principal_id" --role "$role_id" \
    --fill-principal-name false --query 'length(@)' --output tsv 2>&1)"; then
    fail "$description (query failed: $(query_error_summary "$output"))"
  elif [[ "$output" == '0' ]]; then
    pass "$description"
  else
    fail "$description ($output assignment(s) remain); remove with: az role assignment delete --assignee-object-id $principal_id --role $role_id --scope $scope"
  fi
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
  local vault_name="$1" holds_data_protection_key="$2"
  log_step "Key Vault $vault_name"
  assert_query "$vault_name uses RBAC authorization" 'true' keyvault show --name "$vault_name" --query properties.enableRbacAuthorization
  assert_query "$vault_name soft delete enabled" 'true' keyvault show --name "$vault_name" --query properties.enableSoftDelete
  assert_query "$vault_name purge protection enabled" 'true' keyvault show --name "$vault_name" --query properties.enablePurgeProtection
  if [[ "$holds_data_protection_key" == true ]]; then
    assert_query "$vault_name holds RSA key $DATA_PROTECTION_KEY_NAME" 'RSA' keyvault key show --vault-name "$vault_name" --name "$DATA_PROTECTION_KEY_NAME" --query key.kty
  fi
}

verify_storage_account() {
  local account_name="$1" sku="$2" retention_days="$3"
  log_step "Storage account $account_name"
  local account_json
  if ! account_json="$(az_read storage account show --name "$account_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" --output json 2>&1)"; then
    fail "storage account $account_name exists in resource group $ERP_AZURE_RESOURCE_GROUP ($(query_error_summary "$account_json")); $PROVISION_HINT"
    return 0
  fi
  pass "storage account $account_name exists in resource group $ERP_AZURE_RESOURCE_GROUP"

  assert_equals "$account_name kind is StorageV2" 'StorageV2' "$(json_field "$account_json" kind)"
  assert_equals "$account_name sku is $sku" "$sku" "$(json_field "$account_json" sku.name)"
  assert_equals "$account_name access tier is Hot" 'Hot' "$(json_field "$account_json" accessTier)"
  assert_equals "$account_name Shared Key authorization disabled" 'false' "$(json_field "$account_json" allowSharedKeyAccess)"
  assert_equals "$account_name allows Shared Key for no service" 'false' \
    "$(json_eval "$account_json" 'String(JSON.stringify(value.allowSharedKeyAccessForServices ?? null).includes("true"))')" \
    "the template does not set it; turn it off as docs/operations/azure-bootstrap.md section 8 shows"
  assert_equals "$account_name allows no data share" 'false' \
    "$(json_eval "$account_json" 'String(Object.values(value.dataCollaborationPolicyProperties ?? {}).includes(true))')" \
    "the template does not set it; turn it off as docs/operations/azure-bootstrap.md section 8 shows"
  assert_equals "$account_name defaults to Entra ID authorization" 'true' "$(json_field "$account_json" defaultToOAuthAuthentication)"
  assert_equals "$account_name public blob access disallowed" 'false' "$(json_field "$account_json" allowBlobPublicAccess)"
  assert_equals "$account_name cross-tenant replication disallowed" 'false' "$(json_field "$account_json" allowCrossTenantReplication)"
  assert_equals "$account_name minimum TLS version is 1.2" 'TLS1_2' "$(json_field "$account_json" minimumTlsVersion)"
  assert_equals "$account_name accepts HTTPS only" 'true' "$(json_field "$account_json" enableHttpsTrafficOnly)"
  local feature
  for feature in isHnsEnabled isSftpEnabled enableNfsV3 isLocalUserEnabled; do
    assert_equals "$account_name $feature is off" 'false' "$(json_eval "$account_json" 'String(value[args[0]] === true)' "$feature")"
  done
  local endpoint
  endpoint="$(json_field "$account_json" primaryEndpoints.blob)"
  if [[ -n "$endpoint" ]]; then
    pass "$account_name blob endpoint $endpoint"
  else
    fail "$account_name blob endpoint missing"
  fi

  local service_json
  if service_json="$(az_read storage account blob-service-properties show --account-name "$account_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" --output json 2>&1)"; then
    assert_equals "$account_name blob soft delete enabled" 'true' "$(json_eval "$service_json" 'String((value.deleteRetentionPolicy || {}).enabled === true)')"
    assert_equals "$account_name blob soft delete keeps $retention_days days" "$retention_days" "$(json_field "$service_json" deleteRetentionPolicy.days)"
    assert_equals "$account_name container soft delete enabled" 'true' "$(json_eval "$service_json" 'String((value.containerDeleteRetentionPolicy || {}).enabled === true)')"
    assert_equals "$account_name container soft delete keeps $retention_days days" "$retention_days" "$(json_field "$service_json" containerDeleteRetentionPolicy.days)"
    assert_equals "$account_name blob versioning off" 'false' "$(json_eval "$service_json" 'String(value.isVersioningEnabled === true)')"
    assert_equals "$account_name change feed off" 'false' "$(json_eval "$service_json" 'String((value.changeFeed || {}).enabled === true)')"
  else
    fail "$account_name blob service properties readable ($(query_error_summary "$service_json"))"
  fi

  assert_query "$account_name container $ATTACHMENTS_CONTAINER_NAME is private" 'None' storage container-rm show --storage-account "$account_name" \
    --resource-group "$ERP_AZURE_RESOURCE_GROUP" --name "$ATTACHMENTS_CONTAINER_NAME" --query publicAccess
  assert_query "$account_name carries the CanNotDelete lock $STORAGE_LOCK_NAME" 'CanNotDelete' lock show --name "$STORAGE_LOCK_NAME" \
    --resource "$(storage_account_resource_id "$account_name")" --query level
}

verify_storage_listing() {
  local account_name="$1"
  local listing
  if listing="$(az_read storage blob list --account-name "$account_name" --container-name "$ATTACHMENTS_CONTAINER_NAME" --auth-mode login \
    --num-results 1 --output none 2>&1)"; then
    pass "operator lists $account_name/$ATTACHMENTS_CONTAINER_NAME with Entra ID (--auth-mode login)"
  else
    fail "operator lists $account_name/$ATTACHMENTS_CONTAINER_NAME with Entra ID (--auth-mode login) ($(query_error_summary "$listing")); a 403 within 10 minutes of provision.sh is role propagation"
  fi
}

server_expected_roles() {
  local target="$1"
  local production_vault_id secret_name
  production_vault_id="$(vault_resource_id "$ERP_AZURE_KEYVAULT_PROD_NAME")"
  printf '%s\t%s\t%s\n' "$(store_resource_id)" "$ROLE_APP_CONFIGURATION_DATA_READER" 'App Configuration Data Reader on the store'
  if [[ "$target" == runtime ]]; then
    printf '%s\t%s\t%s\n' "$production_vault_id" "$ROLE_KEY_VAULT_SECRETS_USER" "Key Vault Secrets User on $ERP_AZURE_KEYVAULT_PROD_NAME"
    printf '%s\t%s\t%s\n' "$production_vault_id" "$ROLE_KEY_VAULT_CRYPTO_SERVICE_ENCRYPTION_USER" "Key Vault Crypto Service Encryption User on $ERP_AZURE_KEYVAULT_PROD_NAME"
    printf '%s\t%s\t%s\n' "$(storage_container_resource_id "$ERP_AZURE_STORAGE_PROD_NAME" "$ATTACHMENTS_CONTAINER_NAME")" "$ROLE_STORAGE_BLOB_DATA_CONTRIBUTOR" \
      "Storage Blob Data Contributor on container $ERP_AZURE_STORAGE_PROD_NAME/$ATTACHMENTS_CONTAINER_NAME"
    return 0
  fi
  for secret_name in "${ERP_AZURE_MIGRATOR_SECRETS[@]}"; do
    printf '%s\t%s\t%s\n' "$production_vault_id/secrets/$secret_name" "$ROLE_KEY_VAULT_SECRETS_USER" "Key Vault Secrets User on the secret $secret_name of $ERP_AZURE_KEYVAULT_PROD_NAME"
  done
}

verify_server_roles() {
  local target="$1" principal_id="$2"
  local assignments
  if ! assignments="$(az_read role assignment list --all --assignee-object-id "$principal_id" --include-groups \
    --fill-principal-name false --fill-role-definition-name false --query '[].[scope, roleDefinitionId]' --output tsv 2>&1)"; then
    fail "the role assignments of the $target service principal are readable (query failed: $(query_error_summary "$assignments"))"
    return 0
  fi
  local -A held=() expected=()
  local scope role_id description key
  while IFS=$'\t' read -r scope role_id; do
    [[ -n "$scope" ]] || continue
    role_id="${role_id##*/}"
    held["${scope,,} ${role_id,,}"]=1
  done <<< "$assignments"
  while IFS=$'\t' read -r scope role_id description; do
    key="${scope,,} ${role_id,,}"
    expected["$key"]=1
    if [[ -n "${held[$key]:-}" ]]; then
      pass "$target service principal is $description"
    else
      fail "$target service principal is $description (assignment missing); $PROVISION_HINT again"
    fi
  done < <(server_expected_roles "$target")
  local others=0
  for key in "${!held[@]}"; do
    [[ -z "${expected[$key]:-}" ]] || continue
    others=1
    fail "$target service principal holds no other role (it holds the role ${key#* } on ${key%% *}, which provision.sh never grants); remove that assignment"
  done
  (( others )) || pass "$target service principal holds no other role, directly or through a group"
}

grants_data_role() {
  local role_id="$1" provider="$2"
  local found data_actions
  found="$(az_read role definition list --name "$role_id" --query 'length(@)' --output tsv)" || return 2
  [[ "$found" =~ ^[1-9][0-9]*$ ]] || return 2
  data_actions="$(az_read role definition list --name "$role_id" --query '[].permissions[].dataActions[]' --output tsv)" || return 2
  grep -qiE "^(\\*|${provider//./\\.}/)" <<< "$data_actions"
}

verify_resource_access() {
  local resource_name="$1" resource_id="$2" provider="$3" holder="$4" exempt_principal="$5"
  shift 5
  local -A allowed=()
  local entry
  for entry in "$@"; do
    allowed["${entry,,}"]=1
  done
  resource_id="${resource_id,,}"
  local above inside
  if ! above="$(az_read role assignment list --scope "$resource_id" --include-inherited --fill-principal-name false --fill-role-definition-name false \
    --query '[].[scope, principalId, roleDefinitionId]' --output tsv 2>&1)"; then
    fail "the role assignments on and above $resource_name are readable (query failed: $(query_error_summary "$above"))"
    return 0
  fi
  if ! inside="$(az_read role assignment list --all --fill-principal-name false --fill-role-definition-name false \
    --query '[].[scope, principalId, roleDefinitionId]' --output tsv 2>&1)"; then
    fail "the role assignments of the subscription are readable (query failed: $(query_error_summary "$inside"))"
    return 0
  fi
  local scope principal_id role_id status others=0
  local -A seen=()
  while IFS=$'\t' read -r scope principal_id role_id; do
    scope="${scope,,}"
    principal_id="${principal_id,,}"
    role_id="${role_id##*/}"
    role_id="${role_id,,}"
    [[ -n "$scope" && -z "${seen["$scope $principal_id $role_id"]:-}" ]] || continue
    seen["$scope $principal_id $role_id"]=1
    [[ "$principal_id" != "${exempt_principal,,}" && -z "${allowed["$scope $principal_id $role_id"]:-}" ]] || continue
    if [[ "$scope" == "$resource_id" || "$scope" == "$resource_id/"* ]]; then
      others=1
      fail "only $holder holds a role on $resource_name (principal $principal_id holds the role $role_id on $scope)"
    elif [[ "$resource_id" == "$scope/"* || "$scope" == / || "$scope" == /providers/microsoft.management/managementgroups/* ]]; then
      grants_data_role "$role_id" "$provider" && status=0 || status=$?
      if (( status == 0 )); then
        others=1
        fail "only $holder reads $resource_name (principal $principal_id holds the data role $role_id on $scope, above it)"
      elif (( status == 2 )); then
        others=1
        fail "only $holder reads $resource_name (the role $role_id that principal $principal_id holds on $scope cannot be read)"
      fi
    fi
  done <<< "$above"$'\n'"$inside"
  (( others )) || pass "only $holder holds a role on $resource_name or anything in it, and nobody else holds a $provider data role above it"
}

verify_production_storage_network() {
  local account_name="$ERP_AZURE_STORAGE_PROD_NAME"
  log_step "Network rules of $account_name"
  local settings
  if ! settings="$(az_read storage account show --name "$account_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query '[publicNetworkAccess, networkRuleSet.defaultAction, networkRuleSet.bypass, length(networkRuleSet.virtualNetworkRules || `[]`), length(networkRuleSet.resourceAccessRules || `[]`), length(networkRuleSet.ipv6Rules || `[]`), dualStackEndpointPreference.publishIpv6Endpoint || `false`]' \
    --output tsv 2>&1)"; then
    fail "the network rules of $account_name are readable ($(query_error_summary "$settings")); $PROVISION_HINT"
    return 0
  fi
  local public_access default_action bypass virtual_network_rules resource_rules ipv6_rules ipv6_endpoint
  { read -r public_access; read -r default_action; read -r bypass; read -r virtual_network_rules; read -r resource_rules; read -r ipv6_rules; read -r ipv6_endpoint; } <<< "$settings"
  assert_equals "$account_name keeps its public endpoint, behind its rules" 'Enabled' "$public_access" "$PROVISION_HINT"
  assert_equals "$account_name refuses every address its rules do not admit" 'Deny' "$default_action" "$PROVISION_HINT"
  assert_equals "$account_name lets no Azure service past its rules" 'None' "$bypass" "$PROVISION_HINT"
  assert_equals "$account_name has no virtual network rule" '0' "$virtual_network_rules" "$PROVISION_HINT"
  assert_equals "$account_name has no resource instance rule" '0' "$resource_rules" "$PROVISION_HINT"
  assert_equals "$account_name has no IPv6 rule" '0' "$ipv6_rules" "$PROVISION_HINT"
  assert_equals "$account_name publishes no IPv6 endpoint" 'false' "$ipv6_endpoint" \
    "run: az storage account update --name $account_name --resource-group $ERP_AZURE_RESOURCE_GROUP --publish-ipv6-endpoint false"
  local connections name status open_names=''
  if connections="$(production_storage_private_endpoint_connections 2>&1)"; then
    while IFS=$'\t' read -r name status; do
      case "$status" in
        Approved | Pending) open_names+="${open_names:+ }$name" ;;
      esac
    done <<< "$connections"
    if [[ -z "$open_names" ]]; then
      pass "$account_name has no private endpoint connection that is not rejected"
    else
      fail "$account_name has no private endpoint connection that is not rejected (it has $open_names); $(private_endpoint_reject_hint "$open_names")"
    fi
  else
    fail "the private endpoint connections of $account_name are readable ($(query_error_summary "$connections"))"
  fi
  local perimeters
  if perimeters="$(production_storage_perimeter_count 2>&1)"; then
    assert_equals "$account_name belongs to no network security perimeter" '0' "$perimeters" \
      "remove the account on the perimeter's Associated resources page in the Azure portal"
  else
    fail "the network security perimeters of $account_name are readable ($(query_error_summary "$perimeters"))"
  fi
  local rules home_address
  if ! rules="$(production_storage_ip_rules 2>&1)"; then
    fail "the IP rules of $account_name are readable ($(query_error_summary "$rules"))"
    return 0
  fi
  if ! home_address="$(home_rule_address 2>&1)"; then
    fail "the firewall rules of $ERP_AZURE_SQL_SERVER_NAME are readable ($(query_error_summary "$home_address"))"
    return 0
  fi
  local rule count=0
  while IFS= read -r rule; do
    if [[ -n "$rule" ]]; then
      count=$((count + 1))
    fi
  done <<< "$rules"
  if (( count == 1 )) && [[ "$rules" == "$home_address" ]] && is_public_ipv4 "$rules"; then
    pass "$account_name admits one public address, the one the database's rule $SQL_FIREWALL_RULE_NAME admits"
  elif (( count == 0 )); then
    fail "$account_name admits the home's address (it admits none); $HOME_ADDRESS_HINT"
  elif (( count == 1 )) && is_public_ipv4 "$rules"; then
    fail "$account_name admits the one address the database's rule $SQL_FIREWALL_RULE_NAME admits (it admits $rules, the rule ${home_address:-no single address}); $HOME_ADDRESS_HINT"
  else
    fail "$account_name admits one public IPv4 address (it admits $(printf '%s' "$rules" | tr '\n' ' ')); $PROVISION_HINT"
  fi
}

verify_roles() {
  local operator_id="$1" developers_group_id="$2" runtime_id="$3" migrator_id="$4"
  local store_id development_vault_id attachments_container_id attachments_container
  store_id="$(store_resource_id)"
  development_vault_id="$(vault_resource_id "$ERP_AZURE_KEYVAULT_DEV_NAME")"
  attachments_container_id="$(storage_container_resource_id "$ERP_AZURE_STORAGE_DEV_NAME" "$ATTACHMENTS_CONTAINER_NAME")"
  attachments_container="$ERP_AZURE_STORAGE_DEV_NAME/$ATTACHMENTS_CONTAINER_NAME"

  log_step "Role assignments: operator"
  assert_role "operator is App Configuration Data Owner on the store" "$store_id" "$operator_id" "$ROLE_APP_CONFIGURATION_DATA_OWNER"
  local vault_id vault_name
  for vault_name in "$ERP_AZURE_KEYVAULT_DEV_NAME" "$ERP_AZURE_KEYVAULT_PROD_NAME" "$ERP_AZURE_KEYVAULT_OPS_NAME"; do
    vault_id="$(vault_resource_id "$vault_name")"
    assert_role "operator is Key Vault Secrets Officer on $vault_name" "$vault_id" "$operator_id" "$ROLE_KEY_VAULT_SECRETS_OFFICER"
    assert_role "operator is Key Vault Certificates Officer on $vault_name" "$vault_id" "$operator_id" "$ROLE_KEY_VAULT_CERTIFICATES_OFFICER"
    if [[ "$vault_name" != "$ERP_AZURE_KEYVAULT_OPS_NAME" ]]; then
      assert_role "operator is Key Vault Crypto Officer on $vault_name" "$vault_id" "$operator_id" "$ROLE_KEY_VAULT_CRYPTO_OFFICER"
    fi
  done
  assert_role "operator is Storage Blob Data Contributor on container $attachments_container" "$attachments_container_id" "$operator_id" "$ROLE_STORAGE_BLOB_DATA_CONTRIBUTOR"

  log_step "Role assignments: developers group"
  if [[ -n "$developers_group_id" ]]; then
    assert_role "developers group is App Configuration Data Reader on the store" "$store_id" "$developers_group_id" "$ROLE_APP_CONFIGURATION_DATA_READER"
    assert_role "developers group is Key Vault Secrets User on $ERP_AZURE_KEYVAULT_DEV_NAME" "$development_vault_id" "$developers_group_id" "$ROLE_KEY_VAULT_SECRETS_USER"
    assert_role "developers group is Key Vault Crypto Service Encryption User on $ERP_AZURE_KEYVAULT_DEV_NAME" "$development_vault_id" "$developers_group_id" "$ROLE_KEY_VAULT_CRYPTO_SERVICE_ENCRYPTION_USER"
    assert_no_role "developers group is not Key Vault Crypto User on $ERP_AZURE_KEYVAULT_DEV_NAME" "$development_vault_id" "$developers_group_id" "$ROLE_KEY_VAULT_CRYPTO_USER"
    assert_role "developers group is Storage Blob Data Contributor on container $attachments_container" "$attachments_container_id" "$developers_group_id" "$ROLE_STORAGE_BLOB_DATA_CONTRIBUTOR"
  else
    skip "developers group roles: skipped: not configured"
  fi

  local target principal_id
  for target in "${SERVER_TARGETS[@]}"; do
    log_step "Role assignments: $target service principal"
    principal_id="$runtime_id"
    [[ "$target" == runtime ]] || principal_id="$migrator_id"
    if [[ -n "$principal_id" ]]; then
      verify_server_roles "$target" "$principal_id"
    else
      skip "$target service principal roles: skipped: not configured"
    fi
  done

  log_step "Access to $ERP_AZURE_KEYVAULT_OPS_NAME"
  verify_resource_access "$ERP_AZURE_KEYVAULT_OPS_NAME" "$(vault_resource_id "$ERP_AZURE_KEYVAULT_OPS_NAME")" Microsoft.KeyVault 'the operator' "$operator_id"

  log_step "Access to $ERP_AZURE_STORAGE_PROD_NAME"
  verify_resource_access "$ERP_AZURE_STORAGE_PROD_NAME" "$(storage_account_resource_id "$ERP_AZURE_STORAGE_PROD_NAME")" Microsoft.Storage \
    'the runtime service principal' '' \
    "$(storage_container_resource_id "$ERP_AZURE_STORAGE_PROD_NAME" "$ATTACHMENTS_CONTAINER_NAME") $runtime_id $ROLE_STORAGE_BLOB_DATA_CONTRIBUTOR"
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
  assert_query "'$display_name' has no federated credential" '0' ad app federated-credential list --id "$(json_eval "$registration_json" 'value.appId')" \
    --query 'length(@)'
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
  assert_equals "'$display_name' service principal has no certificate of its own" '0' "$(json_eval "$principal_json" '(value.keyCredentials || []).length')"
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
  local expected_logout_url actual_logout_url
  expected_logout_url="$(signin_logout_url "$label")"
  actual_logout_url="$(json_eval "$registration_json" '(value.web && value.web.logoutUrl) || ""')"
  if [[ -n "$expected_logout_url" ]]; then
    assert_equals_exact "'$display_name' front-channel logout URL is $expected_logout_url" "$expected_logout_url" "$actual_logout_url"
  else
    assert_equals_exact "'$display_name' has no front-channel logout URL (its web origin is not set or not https)" '' "$actual_logout_url"
  fi
  if optional_claims_match "$registration_json"; then
    pass "'$display_name' optional claims are exactly the id token claim login_hint ($OPTIONAL_CLAIMS_FILE)"
  else
    fail "'$display_name' optional claims are exactly the id token claim login_hint (got $(json_eval "$registration_json" 'JSON.stringify(value.optionalClaims ?? null)')); $ENTRA_HINT"
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
  local -a scope_ids=("$GRAPH_SCOPE_OPENID" "$GRAPH_SCOPE_PROFILE" "$GRAPH_SCOPE_OFFLINE_ACCESS" "$GRAPH_SCOPE_USER_READ" "$GRAPH_SCOPE_USER_READ_BASIC_ALL")
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
  local requested_roles
  requested_roles="$(json_eval "$registration_json" '(value.requiredResourceAccess || []).filter((r) => String(r.resourceAppId).toLowerCase() === args[0].toLowerCase()).flatMap((r) => r.resourceAccess || []).filter((a) => a.type === "Role").map((a) => String(a.id).toLowerCase())' "$GRAPH_APP_ID")"
  if grep -qix "$GRAPH_ROLE_USER_READ_ALL" <<< "$requested_roles"; then
    pass "'$display_name' requests Microsoft Graph application permission ${GRAPH_ROLE_VALUES[*]}"
  else
    fail "'$display_name' requests Microsoft Graph application permission ${GRAPH_ROLE_VALUES[*]} ($GRAPH_ROLE_USER_READ_ALL not in requiredResourceAccess); $ENTRA_HINT"
  fi
}

verify_application_permission_grant() {
  local principal_id="$1" app_id="$2" display_name="$3"
  local granted
  if ! granted="$(application_permission_assignment_id "$principal_id" 2>&1)"; then
    fail "'$display_name' Microsoft Graph application consent granted (query failed: $(query_error_summary "$granted"))"
    return 0
  fi
  if [[ -n "$granted" ]]; then
    pass "'$display_name' Microsoft Graph application consent covers ${GRAPH_ROLE_VALUES[*]}"
  else
    fail "'$display_name' Microsoft Graph application consent covers ${GRAPH_ROLE_VALUES[*]}; $ENTRA_HINT or, as Privileged Role Administrator: az ad app permission admin-consent --id $app_id"
  fi
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
  local app_id="$1" display_name="$2" vault_name="$3" certificate_name="$4" target="$5"
  local rotate_hint="rotate with: bash scripts/azure/entra.sh --rotate-signin-certificate $target"
  if is_server_target "$target"; then
    rotate_hint="rotate with: bash scripts/azure/entra.sh --rotate-client-certificate $target"
  fi
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
  local candidate others=0
  while IFS= read -r candidate; do
    if [[ -n "$candidate" && "${candidate,,}" != "${thumbprint,,}" ]]; then
      others=$((others + 1))
    fi
  done <<< "$thumbprints"
  if (( others == 0 )); then
    pass "'$display_name' signs in with no other key credential"
  else
    fail "'$display_name' signs in with no other key credential ($others other(s)); once the current certificate is in use, run: bash scripts/azure/entra.sh --prune-old-credentials $target"
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
    verify_application_permission_grant "$principal_id" "$app_id" "$display_name"
    verify_app_role_assignment "$principal_id" "$operator_id" "$display_name"
  fi

  verify_certificate_credential "$app_id" "$display_name" "$vault_name" "$SIGNIN_CERTIFICATE_NAME" "$label"

  assert_appconfig_value "$IDENTITY_TENANT_ID_KEY [$label] is the tenant id" "$IDENTITY_TENANT_ID_KEY" "$label" "$ERP_AZURE_TENANT_ID"
  assert_appconfig_value "$IDENTITY_CLIENT_ID_KEY [$label] is the application id of '$display_name'" "$IDENTITY_CLIENT_ID_KEY" "$label" "$app_id"
  local web_origin
  web_origin="$(signin_web_origin "$label")"
  if [[ -n "$web_origin" ]]; then
    assert_appconfig_value "$IDENTITY_WEB_ORIGIN_KEY [$label] is the web origin $web_origin" "$IDENTITY_WEB_ORIGIN_KEY" "$label" "$web_origin"
  else
    skip "$IDENTITY_WEB_ORIGIN_KEY [$label]: skipped: ERP_AZURE_PRODUCTION_WEB_ORIGIN is not set"
  fi
}

verify_server_registration() {
  local target="$1"
  local display_name certificate_name
  display_name="$(server_app_name "$target")"
  certificate_name="$(client_certificate_name "$target")"
  log_step "Server registration '$display_name' ($target)"
  local registration_json app_id principal_json principal_id
  load_registration "$display_name" registration_json || return 0
  app_id="$(json_eval "$registration_json" 'value.appId')"

  verify_registration_shape "$registration_json" "$display_name"
  assert_equals "'$display_name' has no redirect URI" '0' "$(json_eval "$registration_json" '((value.web && value.web.redirectUris) || []).length')"

  verify_certificate_credential "$app_id" "$display_name" "$ERP_AZURE_KEYVAULT_OPS_NAME" "$certificate_name" "$target"
  local stale
  if ! stale="$(certificate_thumbprint "$ERP_AZURE_KEYVAULT_PROD_NAME" "$certificate_name" 2>&1)"; then
    fail "$ERP_AZURE_KEYVAULT_PROD_NAME, which the runtime reads, holds no $certificate_name (query failed: $(query_error_summary "$stale"))"
  elif [[ -n "$stale" ]]; then
    fail "$ERP_AZURE_KEYVAULT_PROD_NAME, which the runtime reads, holds no $certificate_name (thumbprint $stale is there); once '$display_name' signs in with the certificate in $ERP_AZURE_KEYVAULT_OPS_NAME alone, delete it with: az keyvault certificate delete --vault-name $ERP_AZURE_KEYVAULT_PROD_NAME --name $certificate_name"
  else
    pass "$ERP_AZURE_KEYVAULT_PROD_NAME, which the runtime reads, holds no $certificate_name"
  fi

  load_service_principal "$app_id" "$display_name" principal_json || return 0
  principal_id="$(json_eval "$principal_json" 'value.id')"
  verify_service_principal_shape "$principal_json" "$display_name"
  verify_server_roles "$target" "$principal_id"
}

verify_entra() {
  local operator_id="$1"
  OPERATOR_ID="$operator_id"
  log_step "Resolving Microsoft Graph"
  resolve_graph_permission_ids
  log_info "Microsoft Graph service principal: $GRAPH_SP_OBJECT_ID"
  local label
  for label in "${SIGNIN_LABELS[@]}"; do
    verify_signin_registration "$label" "$operator_id"
  done
  local target
  for target in "${SERVER_TARGETS[@]}"; do
    verify_server_registration "$target"
  done
}

seed_rows() {
  local command="$1" file="$2"
  node "$SEED_FILES_CLI" "$command" "$file" | tr -d '\r'
}

seed_value() {
  local file="$1" key="$2"
  local row row_key value
  while IFS=$'\t' read -r row_key value; do
    if [[ "$row_key" == "$key" ]]; then
      printf '%s' "$value"
      return 0
    fi
  done < <(seed_rows settings "$file")
  return 1
}

assert_seeded_value() {
  local description="$1" key="$2" label="$3" expected="$4"
  local value
  if ! value="$(appconfig_kv_get "$key" "$label" 2>&1)"; then
    fail "$description (query failed: $(query_error_summary "$value"))"
  elif [[ -z "$value" ]]; then
    fail "$description (key absent from the store); $SEED_HINT"
  else
    assert_equals_exact "$description" "$expected" "$value"
  fi
}

verify_seeded_settings() {
  local file="$1" label="$2"
  local -a rows=()
  mapfile -t rows < <(seed_rows settings "$file")
  if (( ${#rows[@]} == 0 )); then
    skip "$file holds no keys"
    return 0
  fi
  local row key expected
  for row in "${rows[@]}"; do
    IFS=$'\t' read -r key expected <<< "$row"
    assert_seeded_value "$key [$(appconfig_label_display "$label")] equals $file" "$key" "$label" "$expected"
  done
}

verify_seeded_feature_flags() {
  local label="$1"
  local -a rows=()
  mapfile -t rows < <(seed_rows flags "$SEED_DIRECTORY/feature-flags.json")
  local row id flag_label enabled expected state
  for row in "${rows[@]}"; do
    IFS=$'\t' read -r id flag_label enabled <<< "$row"
    [[ "$flag_label" == "$label" ]] || continue
    expected='off'
    [[ "$enabled" != true ]] || expected='on'
    if state="$(az_read appconfig feature show --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --feature "$id" --label "$label" --query state --output tsv 2>&1)"; then
      assert_equals "feature flag $id [$label] is $expected" "$expected" "$state"
    else
      fail "feature flag $id [$label] is $expected (query failed: $(query_error_summary "$state")); $SEED_HINT"
    fi
  done
}

verify_seeded_references() {
  local label="$1" vault_name="$2"
  local vault_uri
  if ! vault_uri="$(az_read keyvault show --name "$vault_name" --query properties.vaultUri --output tsv 2>&1)"; then
    fail "vault $vault_name reachable for the $label references ($(query_error_summary "$vault_uri")); $PROVISION_HINT"
    return 0
  fi
  local -a rows=()
  mapfile -t rows < <(seed_rows references "$SEED_DIRECTORY/key-vault-references.json")
  local row key secret ref_labels content_type uri
  for row in "${rows[@]}"; do
    IFS=$'\t' read -r key secret ref_labels <<< "$row"
    if [[ ",$ref_labels," != *",$label,"* ]]; then
      skip "$key [$label] not seeded under this label"
      continue
    fi
    if ! content_type="$(appconfig_kv_query "$key" "$label" 'contentType' 2>&1)"; then
      fail "$key [$label] is a Key Vault reference (query failed: $(query_error_summary "$content_type")); $SEED_HINT"
      continue
    fi
    assert_equals "$key [$label] is a Key Vault reference" "$APPCONFIG_KEYVAULT_REFERENCE_CONTENT_TYPE" "$content_type"
    uri="$(appconfig_kv_reference_uri "$key" "$label")" || uri=''
    assert_equals "$key [$label] points to $secret in $vault_name" "${vault_uri%/}/secrets/$secret" "$uri"
  done
}

verify_label_precedence() {
  log_step "Label precedence"
  local unlabelled labelled expected_default expected_override
  expected_default="$(seed_value "$SEED_DIRECTORY/defaults.json" "$LABEL_PRECEDENCE_KEY")" || { fail "$LABEL_PRECEDENCE_KEY is defined in defaults.json"; return 0; }
  expected_override="$(seed_value "$SEED_DIRECTORY/local-dev.json" "$LABEL_PRECEDENCE_KEY")" || { fail "$LABEL_PRECEDENCE_KEY is overridden in local-dev.json"; return 0; }
  if [[ "$expected_default" == "$expected_override" ]]; then
    fail "local-dev.json gives $LABEL_PRECEDENCE_KEY a value different from defaults.json (both are '$expected_default')"
    return 0
  fi
  pass "local-dev.json overrides $LABEL_PRECEDENCE_KEY ('$expected_default' -> '$expected_override')"
  unlabelled="$(appconfig_kv_get "$LABEL_PRECEDENCE_KEY" '')" || unlabelled=''
  labelled="$(appconfig_kv_get "$LABEL_PRECEDENCE_KEY" local-dev)" || labelled=''
  assert_equals_exact "$LABEL_PRECEDENCE_KEY [(null)] is the default" "$expected_default" "$unlabelled"
  assert_equals_exact "$LABEL_PRECEDENCE_KEY [local-dev] is the override the API loads last" "$expected_override" "$labelled"
  if [[ -n "$labelled" && "$labelled" != "$unlabelled" ]]; then
    pass "the local-dev label overrides the unlabelled default in the store"
  else
    fail "the local-dev label overrides the unlabelled default in the store (unlabelled '${unlabelled:-<none>}', local-dev '${labelled:-<none>}'); $SEED_HINT"
  fi
}

verify_blob_service_uri() {
  local label="$1" account_name="$2"
  local description="$BLOB_SERVICE_URI_KEY [$label] is the blob endpoint of $account_name"
  local endpoint value
  if ! endpoint="$(az_read storage account show --name "$account_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query primaryEndpoints.blob --output tsv 2>&1)"; then
    fail "$description (query failed: $(query_error_summary "$endpoint")); $PROVISION_HINT"
    return 0
  fi
  if ! value="$(appconfig_kv_get "$BLOB_SERVICE_URI_KEY" "$label" 2>&1)"; then
    fail "$description (query failed: $(query_error_summary "$value"))"
  elif [[ -z "$value" ]]; then
    fail "$description (key absent from the store); $PROVISION_HINT"
  else
    assert_equals_exact "$description" "$endpoint" "$value"
  fi
}

verify_data_protection_key_identifier() {
  local label="$1" vault_name="$2"
  local description="$DATA_PROTECTION_KEY_IDENTIFIER_KEY [$label] is the versionless identifier of $DATA_PROTECTION_KEY_NAME in $vault_name"
  local key_id expected value
  if ! key_id="$(az_read keyvault key show --vault-name "$vault_name" --name "$DATA_PROTECTION_KEY_NAME" --query key.kid --output tsv 2>&1)"; then
    fail "$description (query failed: $(query_error_summary "$key_id")); $PROVISION_HINT"
    return 0
  fi
  if ! expected="$(canonical_uri "${key_id%/*}")"; then
    fail "$description (the key id '$key_id' is not a URI)"
    return 0
  fi
  if ! value="$(appconfig_kv_get "$DATA_PROTECTION_KEY_IDENTIFIER_KEY" "$label" 2>&1)"; then
    fail "$description (query failed: $(query_error_summary "$value"))"
  elif [[ -z "$value" ]]; then
    fail "$description (key absent from the store); $PROVISION_HINT"
  else
    assert_equals "$description" "$expected" "$value"
  fi
}

verify_provisioned_endpoints() {
  log_step "Provisioned endpoints"
  verify_blob_service_uri local-dev "$ERP_AZURE_STORAGE_DEV_NAME"
  verify_blob_service_uri production "$ERP_AZURE_STORAGE_PROD_NAME"
  verify_data_protection_key_identifier local-dev "$ERP_AZURE_KEYVAULT_DEV_NAME"
  verify_data_protection_key_identifier production "$ERP_AZURE_KEYVAULT_PROD_NAME"
}

verify_labels() {
  log_step "Seed files"
  if node "$SEED_FILES_CLI" validate > /dev/null 2>&1; then
    pass "$SEED_DIRECTORY passes validation"
  else
    fail "$SEED_DIRECTORY passes validation (run: node $SEED_FILES_CLI validate)"
    return 0
  fi

  log_step "Unlabelled defaults"
  verify_seeded_settings "$SEED_DIRECTORY/defaults.json" ''

  local label vault_name
  for label in "${LABELS[@]}"; do
    log_step "Label $label"
    verify_seeded_settings "$SEED_DIRECTORY/$label.json" "$label"
    verify_seeded_feature_flags "$label"
    vault_name="$ERP_AZURE_KEYVAULT_PROD_NAME"
    [[ "$label" != local-dev ]] || vault_name="$ERP_AZURE_KEYVAULT_DEV_NAME"
    verify_seeded_references "$label" "$vault_name"
  done

  verify_label_precedence
  verify_provisioned_endpoints
  verify_sentinels
}

verify_sql_ipv6_off() {
  local server_url enabled rule_count
  server_url="$(sql_server_resource_id)?api-version=$SQL_API_VERSION"
  if enabled="$(az_read rest --method get --url "$server_url" --query properties.isIPv6Enabled --output tsv 2>&1)"; then
    if [[ "${enabled,,}" == enabled ]]; then
      fail "$ERP_AZURE_SQL_SERVER_NAME has IPv6 off (it is on); $PROVISION_HINT"
    else
      pass "$ERP_AZURE_SQL_SERVER_NAME has IPv6 off"
    fi
  else
    fail "$ERP_AZURE_SQL_SERVER_NAME has IPv6 off (query failed: $(query_error_summary "$enabled"))"
  fi
  if rule_count="$(az_read rest --method get --url "$(sql_server_resource_id)/ipv6FirewallRules?api-version=$SQL_API_VERSION" \
    --query 'length(value)' --output tsv 2>&1)"; then
    assert_equals "$ERP_AZURE_SQL_SERVER_NAME has no IPv6 firewall rule" '0' "$rule_count"
  else
    fail "$ERP_AZURE_SQL_SERVER_NAME has no IPv6 firewall rule (query failed: $(query_error_summary "$rule_count"))"
  fi
}

verify_sql_admin() {
  local operator_id="$1"
  local admins
  if ! admins="$(az_read sql server ad-admin list --resource-group "$ERP_AZURE_RESOURCE_GROUP" --server "$ERP_AZURE_SQL_SERVER_NAME" \
    --query '[].[sid, tenantId]' --output tsv 2>&1)"; then
    fail "$ERP_AZURE_SQL_SERVER_NAME Microsoft Entra admin is the operator (query failed: $(query_error_summary "$admins"))"
    return 0
  fi
  local sid tenant_id
  IFS=$'\t' read -r sid tenant_id <<< "$admins"
  if [[ "$(grep -c . <<< "$admins" || true)" == 1 && "${sid,,}" == "${operator_id,,}" && "${tenant_id,,}" == "${ERP_AZURE_TENANT_ID,,}" ]]; then
    pass "$ERP_AZURE_SQL_SERVER_NAME Microsoft Entra admin is the operator"
  else
    fail "$ERP_AZURE_SQL_SERVER_NAME Microsoft Entra admin is the operator $operator_id of tenant $ERP_AZURE_TENANT_ID (got '${admins:-<none>}')"
  fi
}

verify_sql_server() {
  local operator_id="$1" fqdn_variable="$2"
  printf -v "$fqdn_variable" '%s' ''
  log_step "SQL server $ERP_AZURE_SQL_SERVER_NAME"
  local server_json
  if ! server_json="$(az_read sql server show --name "$ERP_AZURE_SQL_SERVER_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" --output json 2>&1)"; then
    fail "server $ERP_AZURE_SQL_SERVER_NAME exists in resource group $ERP_AZURE_RESOURCE_GROUP ($(query_error_summary "$server_json")); $PROVISION_HINT"
    return 1
  fi
  pass "server $ERP_AZURE_SQL_SERVER_NAME exists in resource group $ERP_AZURE_RESOURCE_GROUP"
  printf -v "$fqdn_variable" '%s' "$(json_field "$server_json" fullyQualifiedDomainName)"
  assert_equals "$ERP_AZURE_SQL_SERVER_NAME is in $ERP_AZURE_LOCATION" "$ERP_AZURE_LOCATION" "$(json_field "$server_json" location)"
  assert_equals "$ERP_AZURE_SQL_SERVER_NAME minimum TLS version is 1.2" '1.2' "$(json_field "$server_json" minimalTlsVersion)"
  assert_equals "$ERP_AZURE_SQL_SERVER_NAME accepts connections over its public endpoint" 'Enabled' "$(json_field "$server_json" publicNetworkAccess)"
  verify_sql_ipv6_off
  assert_equals "$ERP_AZURE_SQL_SERVER_NAME has no private endpoint" '0' "$(json_eval "$server_json" '(value.privateEndpointConnections || []).length')"
  assert_query "$ERP_AZURE_SQL_SERVER_NAME has no virtual network rule" '0' sql server vnet-rule list --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --server "$ERP_AZURE_SQL_SERVER_NAME" --query 'length(@)'
  verify_sql_admin "$operator_id"
  assert_query "$ERP_AZURE_SQL_SERVER_NAME accepts Microsoft Entra authentication only" 'True' sql server ad-only-auth get \
    --resource-group "$ERP_AZURE_RESOURCE_GROUP" --name "$ERP_AZURE_SQL_SERVER_NAME" --query azureAdOnlyAuthentication
  assert_query "$ERP_AZURE_SQL_SERVER_NAME carries the CanNotDelete lock $SQL_SERVER_LOCK_NAME" 'CanNotDelete' lock show --name "$SQL_SERVER_LOCK_NAME" \
    --resource "$(sql_server_resource_id)" --query level
}

verify_sql_firewall() {
  log_step "Firewall of $ERP_AZURE_SQL_SERVER_NAME"
  local rules
  if ! rules="$(firewall_rules_tsv 2>&1)"; then
    fail "firewall rules of $ERP_AZURE_SQL_SERVER_NAME readable ($(query_error_summary "$rules"))"
    return 0
  fi
  local name start end found=0
  while IFS=$'\t' read -r name start end; do
    [[ -n "$name" ]] || continue
    if [[ "$name" == "$SQL_FIREWALL_RULE_NAME" ]]; then
      found=1
      if [[ "$start" == "$end" ]] && is_public_ipv4 "$start"; then
        pass "rule $name admits one public address"
      else
        fail "rule $name admits one public IPv4 address (it admits $start to $end); $HOME_ADDRESS_HINT"
      fi
    else
      fail "no rule besides $SQL_FIREWALL_RULE_NAME admits an address ($name admits $start to $end); remove it by hand: $(lock_hint), az sql server firewall-rule delete --resource-group $ERP_AZURE_RESOURCE_GROUP --server $ERP_AZURE_SQL_SERVER_NAME --name $name, then bash scripts/azure/provision.sh to lock the server again"
    fi
  done <<< "$rules"
  (( found )) || fail "rule $SQL_FIREWALL_RULE_NAME admits the home's public address; $HOME_ADDRESS_HINT"
}

verify_sql_database() {
  log_step "Database $ERP_AZURE_SQL_DATABASE_NAME"
  local database_json
  if ! database_json="$(az_read sql db show --name "$ERP_AZURE_SQL_DATABASE_NAME" --server "$ERP_AZURE_SQL_SERVER_NAME" \
    --resource-group "$ERP_AZURE_RESOURCE_GROUP" --output json 2>&1)"; then
    fail "database $ERP_AZURE_SQL_DATABASE_NAME exists on $ERP_AZURE_SQL_SERVER_NAME ($(query_error_summary "$database_json")); $PROVISION_HINT"
    return 0
  fi
  pass "database $ERP_AZURE_SQL_DATABASE_NAME exists on $ERP_AZURE_SQL_SERVER_NAME"
  assert_equals "$ERP_AZURE_SQL_DATABASE_NAME tier is $SQL_DATABASE_SKU" "$SQL_DATABASE_SKU" "$(json_field "$database_json" sku.name)"
  assert_equals "$ERP_AZURE_SQL_DATABASE_NAME has $SQL_DATABASE_CAPACITY DTU" "$SQL_DATABASE_CAPACITY" "$(json_field "$database_json" sku.capacity)"
  assert_equals "$ERP_AZURE_SQL_DATABASE_NAME holds at most 2 GB" "$SQL_DATABASE_MAX_SIZE_BYTES" "$(json_field "$database_json" maxSizeBytes)"
  assert_equals "$ERP_AZURE_SQL_DATABASE_NAME backups are geo-redundant, to the paired region in India" "$SQL_BACKUP_STORAGE_REDUNDANCY" \
    "$(json_field "$database_json" requestedBackupStorageRedundancy)"
  assert_query "$ERP_AZURE_SQL_DATABASE_NAME restores to any point of the last $SQL_RESTORE_DAYS days" "$SQL_RESTORE_DAYS" sql db str-policy show \
    --resource-group "$ERP_AZURE_RESOURCE_GROUP" --server "$ERP_AZURE_SQL_SERVER_NAME" --name "$ERP_AZURE_SQL_DATABASE_NAME" --query retentionDays
}

verify_connection_string_secrets() {
  local server_fqdn="$1"
  log_step "Connection strings in $ERP_AZURE_KEYVAULT_PROD_NAME"
  local expected secret_name value
  expected="$(database_connection_string "$server_fqdn")"
  for secret_name in "${SQL_CONNECTION_STRING_SECRETS[@]}"; do
    if ! value="$(az_read keyvault secret show --vault-name "$ERP_AZURE_KEYVAULT_PROD_NAME" --name "$secret_name" --query value --output tsv 2>&1)"; then
      fail "$secret_name exists in $ERP_AZURE_KEYVAULT_PROD_NAME ($(query_error_summary "$value")); $PROVISION_HINT"
    elif [[ "$value" == "$expected" ]]; then
      pass "$secret_name is the production database's connection string"
    else
      fail "$secret_name is the production database's connection string (the stored value differs and is not shown); $PROVISION_HINT"
    fi
  done
}

verify_sql_users() {
  log_step "Users of $ERP_AZURE_SQL_DATABASE_NAME"
  local runtime_app_id migrator_app_id
  runtime_app_id="$(service_principal_lookup "$ERP_AZURE_APP_RUNTIME_NAME" appId)"
  migrator_app_id="$(service_principal_lookup "$ERP_AZURE_APP_MIGRATOR_NAME" appId)"
  if [[ -z "$runtime_app_id" || -z "$migrator_app_id" ]]; then
    fail "the runtime and migrator service principals exist; $ENTRA_HINT"
    return 0
  fi
  resolve_sqlcmd
  local result
  result="$(probe_sql_access)"
  case "$result" in
    answered*) pass "this computer reaches the database as the operator" ;;
    refused*)
      fail "this computer reaches the database (the firewall refuses this computer's address); $HOME_ADDRESS_HINT"
      return 0
      ;;
    signin-refused*)
      fail "this computer reaches the database (the server refused the operator's sign-in: ${result#signin-refused })"
      return 0
      ;;
    *)
      fail "this computer reaches the database (${result#unreachable })"
      return 0
      ;;
  esac
  local output
  if ! output="$(run_users_script 1 "$runtime_app_id" "$migrator_app_id")"; then
    fail "the users and rights hold ($(query_error_summary "$output")); $DATABASE_HINT"
    return 0
  fi
  local line outstanding=0
  while IFS= read -r line; do
    case "$line" in
      change:*)
        fail "nothing is left for database.sh to change (it would ${line#change: }); $DATABASE_HINT"
        outstanding=1
        ;;
      pending:*)
        fail "${line#pending: }"
        outstanding=1
        ;;
      ok:*) pass "${line#ok: }" ;;
    esac
  done <<< "$output"
  (( outstanding )) || pass "users [$ERP_AZURE_APP_RUNTIME_NAME] and [$ERP_AZURE_APP_MIGRATOR_NAME] are the service principals, with exactly their roles"
}

verify_database() {
  local operator_id="$1"
  local server_fqdn
  verify_sql_server "$operator_id" server_fqdn || return 0
  SQL_SERVER_HOST="$server_fqdn"
  verify_sql_firewall
  verify_sql_database
  verify_connection_string_secrets "$server_fqdn"
  verify_sql_users
}

verify_provisioning() {
  local operator_id="$1"
  local developers_group_id runtime_id migrator_id
  developers_group_id="$(developers_group_object_id)"
  runtime_id="$(service_principal_lookup "$ERP_AZURE_APP_RUNTIME_NAME" id)"
  migrator_id="$(service_principal_lookup "$ERP_AZURE_APP_MIGRATOR_NAME" id)"

  verify_store
  verify_vault "$ERP_AZURE_KEYVAULT_DEV_NAME" true
  verify_vault "$ERP_AZURE_KEYVAULT_PROD_NAME" true
  verify_vault "$ERP_AZURE_KEYVAULT_OPS_NAME" false
  verify_storage_account "$ERP_AZURE_STORAGE_DEV_NAME" "$DEVELOPMENT_STORAGE_SKU" "$DEVELOPMENT_STORAGE_RETENTION_DAYS"
  verify_storage_listing "$ERP_AZURE_STORAGE_DEV_NAME"
  verify_storage_account "$ERP_AZURE_STORAGE_PROD_NAME" "$PRODUCTION_STORAGE_SKU" "$PRODUCTION_STORAGE_RETENTION_DAYS"
  verify_production_storage_network
  verify_roles "$operator_id" "$developers_group_id" "$runtime_id" "$migrator_id"
  verify_sentinels
}

main() {
  local -a arguments=()
  local argument
  for argument in "$@"; do
    case "$argument" in
      --entra) ENTRA_MODE=1 ;;
      --labels) LABELS_MODE=1 ;;
      --database) DATABASE_MODE=1 ;;
      *) arguments+=("$argument") ;;
    esac
  done
  (( ENTRA_MODE + LABELS_MODE + DATABASE_MODE <= 1 )) || die "--entra, --labels and --database are separate runs; pass one of them"
  parse_args "${arguments[@]}"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az node

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  log_step "Resolving principals"
  local operator_id
  operator_id="$(signed_in_user_object_id)"

  if (( ENTRA_MODE )); then
    verify_entra "$operator_id"
  elif (( LABELS_MODE )); then
    verify_labels
  elif (( DATABASE_MODE )); then
    verify_database "$operator_id"
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
