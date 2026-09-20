#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/verify.sh [--params <file>]

Reads the provisioned Azure resources back and asserts the documented state:
the App Configuration store, both Key Vaults, the data-protection keys, every
row of the role matrix and the labelled Erp:Sentinel keys. Never writes.

Options:
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

ACCEPTS_DRY_RUN=0

source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/appconfig.sh"

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

FAILURES=0

pass() { printf '[ok]   %s\n' "$*"; }

fail() {
  printf '[FAIL] %s\n' "$*"
  FAILURES=$((FAILURES + 1))
}

skip() { printf '[skip] %s\n' "$*"; }

read_value() {
  local -n target="$1"
  shift
  if target="$(az_read "$@" --output tsv 2> /dev/null)"; then
    return 0
  fi
  target=''
  return 1
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
  local actual
  if read_value actual "$@"; then
    assert_equals "$description" "$expected" "$actual"
  else
    fail "$description (query failed)"
  fi
}

assert_role() {
  local description="$1" scope="$2" principal_id="$3" role_id="$4"
  local count
  if read_value count role assignment list --scope "$scope" --assignee-object-id "$principal_id" --role "$role_id" \
    --fill-principal-name false --query 'length(@)'; then
    assert_equals "$description" '1' "$count"
  else
    fail "$description (query failed)"
  fi
}

verify_store() {
  log_step "App Configuration store $ERP_AZURE_APPCONFIG_NAME"
  local store_json
  if ! store_json="$(az_read appconfig show --name "$ERP_AZURE_APPCONFIG_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" --output json 2> /dev/null)"; then
    fail "store exists in resource group $ERP_AZURE_RESOURCE_GROUP"
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
    if value="$(appconfig_kv_get "$SENTINEL_KEY" "$label" 2> /dev/null)" && [[ -n "$value" ]]; then
      pass "$SENTINEL_KEY [$label] present ($value)"
    else
      fail "$SENTINEL_KEY [$label] present"
    fi
  done
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  load_params
  require_command az node

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  log_step "Resolving principals"
  local operator_id developers_group_id runtime_id
  operator_id="$(signed_in_user_object_id)"
  developers_group_id="$(developers_group_object_id)"
  runtime_id="$(runtime_service_principal_object_id)"

  verify_store
  verify_vault "$ERP_AZURE_KEYVAULT_DEV_NAME"
  verify_vault "$ERP_AZURE_KEYVAULT_PROD_NAME"
  verify_roles "$operator_id" "$developers_group_id" "$runtime_id"
  verify_sentinels

  log_step "Result"
  if (( FAILURES > 0 )); then
    printf '[FAIL] %d check(s) failed\n' "$FAILURES"
    exit 1
  fi
  printf '[ok]   all checks passed\n'
}

main "$@"
