#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/provision.sh [--dry-run] [--params <file>]

Creates or converges the ERP-AI-Pro resource group, the App Configuration store,
both Key Vaults with their data-protection keys, the operations Key Vault (the
client certificates of the server identities, which no application reads), the
development and production storage accounts with their attachments containers
and delete locks (the production one geo-redundant and reachable only from the
address the database'"'"'s firewall rule home admits), the production Azure SQL
server (Microsoft Entra-only, the operator as its Microsoft Entra admin, delete
lock) and its Basic database, the role assignments (the migrator reads only the
production database'"'"'s connection strings), the labelled Erp:Sentinel keys,
Erp:Platform:Attachments:BlobServiceUri under each label (bumping that label'"'"'s
sentinel only when its endpoint changed), Erp:Platform:DataProtection:KeyIdentifier
under the local-dev and production labels (the versionless identifier of the
data-protection key in the matching vault; no sentinel bump, because the API
reads it only when it starts) and the connection-string secrets of the production
database in the production vault. The home'"'"'s address is home-address.sh'"'"'s: a
deployment sets the production storage account'"'"'s rule to the address of the
database'"'"'s rule home, keeps the account'"'"'s own single public address while that
rule admits none, and stops before it changes anything while the rule admits an
address that is not public, or none while the account admits more than one
public address. Safe to run repeatedly; deletes or purges nothing but a role of
the migrator on the whole production vault.

Options:
  --dry-run         Print the what-if result and every command that would run; write nothing.
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=lib/appconfig.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/appconfig.sh"
# shellcheck source=lib/sql.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/sql.sh"
# shellcheck source=lib/storage.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/storage.sh"

readonly SENTINEL_KEY='Erp:Sentinel'
readonly LABELS=(local-dev production)
readonly TEMPLATE_FILE='scripts/azure/bicep/main.bicep'
readonly BLOB_SERVICE_URI_KEY='Erp:Platform:Attachments:BlobServiceUri'
readonly DATA_PROTECTION_KEY_NAME='Erp--Platform--DataProtection--Key'
readonly DATA_PROTECTION_KEY_IDENTIFIER_KEY='Erp:Platform:DataProtection:KeyIdentifier'
readonly ROLE_KEY_VAULT_SECRETS_USER='4633458b-17de-408a-b874-0445c86b69e6'

deployment_output() {
  local outputs_json="$1" name="$2"
  local value
  value="$(json_field "$outputs_json" "$name.value")"
  [[ -n "$value" ]] || die "deployment output '$name' missing"
  printf '%s' "$value"
}

deployment_output_uri() {
  local outputs_json="$1" name="$2"
  local value uri
  value="$(deployment_output "$outputs_json" "$name")"
  uri="$(canonical_uri "$value")" || die "deployment output '$name' is not an absolute URI ('$value')"
  printf '%s' "$uri"
}

ensure_data_protection_key_identifier() {
  local label="$1" key_uri="$2"
  local previous
  previous="$(retry -- appconfig_kv_get "$DATA_PROTECTION_KEY_IDENTIFIER_KEY" "$label")"
  ensure_kv "$DATA_PROTECTION_KEY_IDENTIFIER_KEY" "$label" "$key_uri"
  if [[ "$previous" != "$key_uri" ]]; then
    log_info "$DATA_PROTECTION_KEY_IDENTIFIER_KEY [$label] was written; the API reads it only when it starts, so restart any API running against $label"
  fi
}

ensure_sentinel() {
  local label="$1"
  local current
  current="$(retry -- appconfig_kv_get "$SENTINEL_KEY" "$label")"
  if [[ -n "$current" ]]; then
    log_info "$SENTINEL_KEY [$label] unchanged ($current)"
    return 0
  fi
  ensure_kv "$SENTINEL_KEY" "$label" "$(utc_timestamp)"
}

storage_account_in_group() {
  local account_name="$1"
  local count
  count="$(az_read storage account list --resource-group "$ERP_AZURE_RESOURCE_GROUP" --query "length([?name=='$account_name'])" --output tsv)" \
    || die "cannot list the storage accounts of resource group '$ERP_AZURE_RESOURCE_GROUP'"
  [[ "$count" == 1 ]]
}

ensure_storage_account_name() {
  local parameter="$1" group_exists="$2"
  local account_name="${!parameter}"
  if [[ "$group_exists" == "true" ]] && storage_account_in_group "$account_name"; then
    log_info "exists"
    return 0
  fi
  local result
  result="$(az_read storage account check-name --name "$account_name" --output json)" \
    || die "cannot check whether the storage account name '$account_name' is available (see the error above); MissingSubscriptionRegistration means the subscription has never used storage: run az provider register --namespace Microsoft.Storage --wait, then re-run"
  if [[ "$(json_field "$result" nameAvailable)" == "true" ]]; then
    log_info "name available"
    return 0
  fi
  die "storage account name '$account_name' cannot be used ($(json_field "$result" reason): $(json_field "$result" message)); set another $parameter in $PARAMS_FILE"
}

ensure_sql_server_name() {
  local group_exists="$1"
  if [[ "$group_exists" == "true" ]] && sql_server_exists; then
    log_info "exists"
    return 0
  fi
  local body_file result
  body_file="$(azure_temp_file sql-server-name)"
  printf '{"name":"%s","type":"Microsoft.Sql/servers"}' "$ERP_AZURE_SQL_SERVER_NAME" > "$body_file"
  result="$(az_read rest --method post --url "/subscriptions/$ERP_AZURE_SUBSCRIPTION_ID/providers/Microsoft.Sql/checkNameAvailability?api-version=$SQL_API_VERSION" \
    --body "@$body_file" --output json)" || { rm -f -- "$body_file"; die "cannot check whether the SQL server name '$ERP_AZURE_SQL_SERVER_NAME' is available (see the error above); MissingSubscriptionRegistration means the subscription has never used Azure SQL: run az provider register --namespace Microsoft.Sql --wait, then re-run"; }
  rm -f -- "$body_file"
  if [[ "$(json_field "$result" available)" == "true" ]]; then
    log_info "name available"
    return 0
  fi
  die "SQL server name '$ERP_AZURE_SQL_SERVER_NAME' cannot be used ($(json_field "$result" reason): $(json_field "$result" message)); set another ERP_AZURE_SQL_SERVER_NAME in $PARAMS_FILE"
}

require_unchanged_sql_admin() {
  local operator_id="$1" group_exists="$2"
  [[ "$group_exists" == "true" ]] && sql_server_exists || return 0
  local admin_id
  admin_id="$(az_read sql server ad-admin list --resource-group "$ERP_AZURE_RESOURCE_GROUP" --server "$ERP_AZURE_SQL_SERVER_NAME" \
    --query '[0].sid' --output tsv)" || die "cannot read the Microsoft Entra admin of $ERP_AZURE_SQL_SERVER_NAME"
  if [[ -n "$admin_id" && "${admin_id,,}" != "${operator_id,,}" ]]; then
    die "the Microsoft Entra admin of $ERP_AZURE_SQL_SERVER_NAME is $admin_id, not the signed-in operator $operator_id, and a deployment would hand the server to the operator; sign in as that admin, or change the admin deliberately with az sql server ad-admin create"
  fi
}

existing_secret_names() {
  local vault_name="$1"
  shift
  az_read keyvault show --name "$vault_name" --query name --output tsv > /dev/null 2>&1 || return 0
  local names='' secret_name count
  for secret_name in "$@"; do
    count="$(az_read keyvault secret list --vault-name "$vault_name" --query "length([?name=='$secret_name'])" --output tsv)" \
      || die "cannot list the secrets of $vault_name"
    if [[ "$count" == 1 ]]; then
      names+="${names:+,}$secret_name"
    fi
  done
  printf '%s' "$names"
}

vault_secret_value() {
  local vault_name="$1" secret_name="$2"
  local count
  count="$(az_read keyvault secret list --vault-name "$vault_name" --query "length([?name=='$secret_name'])" --output tsv)" || return 1
  [[ "$count" == 1 ]] || return 0
  az_read keyvault secret show --vault-name "$vault_name" --name "$secret_name" --query value --output tsv
}

ensure_vault_secret() {
  local vault_name="$1" secret_name="$2" value="$3"
  local current
  current="$(retry -- vault_secret_value "$vault_name" "$secret_name")"
  if [[ "$current" == "$value" ]]; then
    log_info "$secret_name in $vault_name unchanged"
    return 0
  fi
  local value_file
  value_file="$(azure_temp_file connection-string)"
  printf '%s' "$value" > "$value_file"
  if ! az keyvault secret set --vault-name "$vault_name" --name "$secret_name" --file "$value_file" --encoding utf-8 \
    --content-type text/plain --output none --only-show-errors; then
    rm -f -- "$value_file"
    die "cannot write $secret_name to $vault_name (see the error above)"
  fi
  rm -f -- "$value_file"
  log_info "$secret_name written to $vault_name"
}

revoke_vault_wide_secrets_role() {
  local principal_id="$1" vault_name="$2"
  [[ -n "$principal_id" ]] || return 0
  local scope assignments id assignment_scope found=0
  scope="$(vault_resource_id "$vault_name")"
  assignments="$(az_read role assignment list --scope "$scope" --assignee-object-id "$principal_id" --role "$ROLE_KEY_VAULT_SECRETS_USER" \
    --fill-principal-name false --query '[].[id, scope]' --output tsv)" || die "cannot list the role assignments of $vault_name"
  while IFS=$'\t' read -r id assignment_scope; do
    [[ -n "$id" && "${assignment_scope,,}" == "${scope,,}" ]] || continue
    found=1
    log_info "removing the migrator's Key Vault Secrets User on the whole of $vault_name"
    run az role assignment delete --ids "$id" --only-show-errors
  done <<< "$assignments"
  (( found )) || log_info "the migrator holds no role on the whole of $vault_name"
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  load_params
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  require_command az node mktemp
  [[ -f "$TEMPLATE_FILE" ]] || die "template '$TEMPLATE_FILE' not found"

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  log_step "Resolving principals"
  local operator_id operator_login developers_group_id runtime_id migrator_id
  operator_id="$(signed_in_user_object_id)"
  operator_login="$(az_read ad signed-in-user show --query userPrincipalName --output tsv)" || die "cannot read the signed-in user's sign-in name"
  log_info "operator: $operator_id ($operator_login), the Microsoft Entra admin of $ERP_AZURE_SQL_SERVER_NAME"
  developers_group_id="$(developers_group_object_id)"
  if [[ -n "$developers_group_id" ]]; then
    log_info "developers group '$ERP_AZURE_DEVELOPERS_GROUP': $developers_group_id"
  else
    log_info "developers group: not configured; developer roles skipped"
  fi
  runtime_id="$(service_principal_lookup "$ERP_AZURE_APP_RUNTIME_NAME" id)"
  if [[ -n "$runtime_id" ]]; then
    log_info "runtime service principal '$ERP_AZURE_APP_RUNTIME_NAME': $runtime_id"
  else
    log_info "runtime service principal '$ERP_AZURE_APP_RUNTIME_NAME' not found; runtime roles skipped until entra.sh has run"
  fi
  migrator_id="$(service_principal_lookup "$ERP_AZURE_APP_MIGRATOR_NAME" id)"
  if [[ -n "$migrator_id" ]]; then
    log_info "migrator service principal '$ERP_AZURE_APP_MIGRATOR_NAME': $migrator_id"
  else
    log_info "migrator service principal '$ERP_AZURE_APP_MIGRATOR_NAME' not found; migrator roles skipped until entra.sh has run"
  fi

  log_step "Resource group $ERP_AZURE_RESOURCE_GROUP"
  local group_exists
  group_exists="$(az_read group exists --name "$ERP_AZURE_RESOURCE_GROUP" --output tsv)"
  if [[ "$group_exists" == "true" ]]; then
    log_info "exists"
  else
    run az group create --name "$ERP_AZURE_RESOURCE_GROUP" --location "$ERP_AZURE_LOCATION" \
      --tags project=erp-ai-pro managedBy=scripts/azure --output none --only-show-errors
    (( DRY_RUN )) || log_info "created"
  fi

  log_step "Storage account $ERP_AZURE_STORAGE_DEV_NAME"
  ensure_storage_account_name ERP_AZURE_STORAGE_DEV_NAME "$group_exists"

  log_step "Storage account $ERP_AZURE_STORAGE_PROD_NAME"
  ensure_storage_account_name ERP_AZURE_STORAGE_PROD_NAME "$group_exists"

  log_step "SQL server $ERP_AZURE_SQL_SERVER_NAME"
  ensure_sql_server_name "$group_exists"
  require_unchanged_sql_admin "$operator_id" "$group_exists"

  log_step "The home's address"
  local home_address='' storage_rules='' storage_address=''
  if [[ "$group_exists" == "true" ]]; then
    if sql_server_exists; then
      home_address="$(home_rule_address)"
    fi
    if production_storage_exists; then
      storage_rules="$(production_storage_ip_rules)"
    fi
  fi
  if [[ -n "$home_address" ]]; then
    is_public_ipv4 "$home_address" \
      || die "the database's rule $SQL_FIREWALL_RULE_NAME admits $home_address, which is not a public IPv4 address, so the storage account $ERP_AZURE_STORAGE_PROD_NAME could not take it; run first: $HOME_ADDRESS_HINT"
    storage_address="$home_address"
    if [[ -n "$storage_rules" && "$storage_rules" != "$home_address" ]]; then
      log_warn "$ERP_AZURE_STORAGE_PROD_NAME admits $(printf '%s' "$storage_rules" | tr '\n' ' '); the deployment sets its rule to $home_address alone, the address of the database's rule $SQL_FIREWALL_RULE_NAME"
    fi
  elif [[ -n "$storage_rules" && "$storage_rules" != *$'\n'* ]] && is_public_ipv4 "$storage_rules"; then
    storage_address="$storage_rules"
    log_warn "the database's rule $SQL_FIREWALL_RULE_NAME admits no single address, so the deployment keeps $ERP_AZURE_STORAGE_PROD_NAME on $storage_address; then run: $HOME_ADDRESS_HINT"
  elif [[ -n "$storage_rules" ]]; then
    die "the database's rule $SQL_FIREWALL_RULE_NAME admits no single address and $ERP_AZURE_STORAGE_PROD_NAME admits $(printf '%s' "$storage_rules" | tr '\n' ' '), so the deployment would leave the account admitting none; remove every rule but the home's by hand (az storage account network-rule remove --resource-group $ERP_AZURE_RESOURCE_GROUP --account-name $ERP_AZURE_STORAGE_PROD_NAME --ip-address <address>), then run this script again"
  fi
  log_info "$ERP_AZURE_STORAGE_PROD_NAME admits ${storage_address:-no address until bash scripts/azure/home-address.sh points the rule $SQL_FIREWALL_RULE_NAME of the database at one}"

  log_step "The migrator's secrets in $ERP_AZURE_KEYVAULT_PROD_NAME"
  local migrator_secret_names=''
  if [[ -n "$migrator_id" ]]; then
    migrator_secret_names="$(existing_secret_names "$ERP_AZURE_KEYVAULT_PROD_NAME" "${ERP_AZURE_MIGRATOR_SECRETS[@]}")"
    log_info "Key Vault Secrets User on: ${migrator_secret_names:-none yet; a secret is granted on the run after the one that writes it}"
  else
    log_info "skipped until entra.sh has created the migrator"
  fi

  local -a template_parameters=(
    "location=$ERP_AZURE_LOCATION"
    "configurationStoreName=$ERP_AZURE_APPCONFIG_NAME"
    "configurationStoreSku=$ERP_AZURE_APPCONFIG_SKU"
    "developmentKeyVaultName=$ERP_AZURE_KEYVAULT_DEV_NAME"
    "productionKeyVaultName=$ERP_AZURE_KEYVAULT_PROD_NAME"
    "operationsKeyVaultName=$ERP_AZURE_KEYVAULT_OPS_NAME"
    "developmentStorageAccountName=$ERP_AZURE_STORAGE_DEV_NAME"
    "productionStorageAccountName=$ERP_AZURE_STORAGE_PROD_NAME"
    "productionStorageHomeAddress=$storage_address"
    "productionSqlServerName=$ERP_AZURE_SQL_SERVER_NAME"
    "productionSqlDatabaseName=$ERP_AZURE_SQL_DATABASE_NAME"
    "operatorPrincipalId=$operator_id"
    "operatorLogin=$operator_login"
    "developersGroupPrincipalId=$developers_group_id"
    "runtimePrincipalId=$runtime_id"
    "migratorPrincipalId=$migrator_id"
    "migratorSecretNames=$migrator_secret_names"
  )

  log_step "Deployment preview (what-if)"
  if [[ "$group_exists" == "true" ]] || (( ! DRY_RUN )); then
    az deployment group what-if --resource-group "$ERP_AZURE_RESOURCE_GROUP" --mode Incremental \
      --template-file "$TEMPLATE_FILE" --parameters "${template_parameters[@]}" --only-show-errors
  else
    log_info "what-if skipped until the resource group exists"
  fi

  local deployment_name
  deployment_name="erp-ai-pro-$(date -u +%Y%m%d%H%M%S)"
  local store_endpoint="https://$ERP_AZURE_APPCONFIG_NAME.azconfig.io"
  local development_vault_uri="https://$ERP_AZURE_KEYVAULT_DEV_NAME.vault.azure.net/"
  local production_vault_uri="https://$ERP_AZURE_KEYVAULT_PROD_NAME.vault.azure.net/"
  local operations_vault_uri="https://$ERP_AZURE_KEYVAULT_OPS_NAME.vault.azure.net/"
  local -A attachments_blob_endpoints=(
    [local-dev]="https://$ERP_AZURE_STORAGE_DEV_NAME.blob.core.windows.net/"
    [production]="https://$ERP_AZURE_STORAGE_PROD_NAME.blob.core.windows.net/"
  )
  local development_data_protection_key_uri="${development_vault_uri}keys/$DATA_PROTECTION_KEY_NAME"
  local production_data_protection_key_uri="${production_vault_uri}keys/$DATA_PROTECTION_KEY_NAME"
  local sql_server_fqdn="$ERP_AZURE_SQL_SERVER_NAME.database.windows.net"
  local secret_name label

  log_step "Deployment $deployment_name"
  if (( DRY_RUN )); then
    run az deployment group create --name "$deployment_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
      --mode Incremental --template-file "$TEMPLATE_FILE" --parameters "${template_parameters[@]}" \
      --query properties.outputs --output json --only-show-errors
    log_step "Data plane"
    for label in "${LABELS[@]}"; do
      run az appconfig kv set --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --key "$BLOB_SERVICE_URI_KEY" \
        --label "$label" --value "${attachments_blob_endpoints[$label]}" --yes --output none --only-show-errors
    done
    run az appconfig kv set --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --key "$DATA_PROTECTION_KEY_IDENTIFIER_KEY" \
      --label local-dev --value "$development_data_protection_key_uri" --yes --output none --only-show-errors
    run az appconfig kv set --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --key "$DATA_PROTECTION_KEY_IDENTIFIER_KEY" \
      --label production --value "$production_data_protection_key_uri" --yes --output none --only-show-errors
    for label in "${LABELS[@]}"; do
      run az appconfig kv set --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --key "$SENTINEL_KEY" --label "$label" \
        --value "$(utc_timestamp)" --yes --output none --only-show-errors
    done
    for secret_name in "${SQL_CONNECTION_STRING_SECRETS[@]}"; do
      run az keyvault secret set --vault-name "$ERP_AZURE_KEYVAULT_PROD_NAME" --name "$secret_name" --file '<file holding the connection string>' \
        --encoding utf-8 --content-type text/plain --output none --only-show-errors
    done
    log_info "$BLOB_SERVICE_URI_KEY is written under each label only when it differs from the deployment output"
    log_info "$DATA_PROTECTION_KEY_IDENTIFIER_KEY is written under each label only when it differs from the deployment output, without a sentinel bump: the API reads it only when it starts"
    log_info "each sentinel is written when absent, and bumped when the label's $BLOB_SERVICE_URI_KEY changed"
    log_info "each connection-string secret is written only when it differs from: $(database_connection_string "$sql_server_fqdn")"
  else
    local outputs_json
    outputs_json="$(az_read deployment group create --name "$deployment_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
      --mode Incremental --template-file "$TEMPLATE_FILE" --parameters "${template_parameters[@]}" \
      --query properties.outputs --output json)"
    store_endpoint="$(deployment_output "$outputs_json" configurationStoreEndpoint)"
    development_vault_uri="$(deployment_output "$outputs_json" developmentKeyVaultUri)"
    production_vault_uri="$(deployment_output "$outputs_json" productionKeyVaultUri)"
    operations_vault_uri="$(deployment_output "$outputs_json" operationsKeyVaultUri)"
    attachments_blob_endpoints[local-dev]="$(deployment_output "$outputs_json" developmentAttachmentsBlobEndpoint)"
    attachments_blob_endpoints[production]="$(deployment_output "$outputs_json" productionAttachmentsBlobEndpoint)"
    development_data_protection_key_uri="$(deployment_output_uri "$outputs_json" developmentDataProtectionKeyUri)"
    production_data_protection_key_uri="$(deployment_output_uri "$outputs_json" productionDataProtectionKeyUri)"
    sql_server_fqdn="$(deployment_output "$outputs_json" productionSqlServerFqdn)"
    log_info "deployed"

    log_step "Data plane"
    local previous_blob_service_uri
    ensure_data_protection_key_identifier local-dev "$development_data_protection_key_uri"
    ensure_data_protection_key_identifier production "$production_data_protection_key_uri"
    for label in "${LABELS[@]}"; do
      previous_blob_service_uri="$(retry -- appconfig_kv_get "$BLOB_SERVICE_URI_KEY" "$label")"
      ensure_kv "$BLOB_SERVICE_URI_KEY" "$label" "${attachments_blob_endpoints[$label]}"
      if [[ "$previous_blob_service_uri" != "${attachments_blob_endpoints[$label]}" ]]; then
        log_info "$BLOB_SERVICE_URI_KEY [$label] changed; bumping $SENTINEL_KEY [$label]; an API builds its storage clients when it starts, so restart a running one to use the new endpoint"
        ensure_kv "$SENTINEL_KEY" "$label" "$(utc_timestamp)"
      else
        ensure_sentinel "$label"
      fi
    done
    for secret_name in "${SQL_CONNECTION_STRING_SECRETS[@]}"; do
      ensure_vault_secret "$ERP_AZURE_KEYVAULT_PROD_NAME" "$secret_name" "$(database_connection_string "$sql_server_fqdn")"
    done
  fi

  log_step "The migrator's role on the whole of $ERP_AZURE_KEYVAULT_PROD_NAME"
  revoke_vault_wide_secrets_role "$migrator_id" "$ERP_AZURE_KEYVAULT_PROD_NAME"

  log_step "Summary"
  printf 'APPCONFIG_ENDPOINT=%s\n' "$store_endpoint"
  printf 'KEYVAULT_DEV_URI=%s\n' "$development_vault_uri"
  printf 'KEYVAULT_PROD_URI=%s\n' "$production_vault_uri"
  printf 'KEYVAULT_OPS_URI=%s\n' "$operations_vault_uri"
  printf 'DATA_PROTECTION_KEY_URI_DEV=%s\n' "$development_data_protection_key_uri"
  printf 'DATA_PROTECTION_KEY_URI_PROD=%s\n' "$production_data_protection_key_uri"
  printf 'ATTACHMENTS_BLOB_ENDPOINT_DEV=%s\n' "${attachments_blob_endpoints[local-dev]}"
  printf 'ATTACHMENTS_BLOB_ENDPOINT_PROD=%s\n' "${attachments_blob_endpoints[production]}"
  printf 'SQL_SERVER_PROD=%s\n' "$sql_server_fqdn"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed"
  fi
  log_info "a new Storage Blob Data Contributor assignment on an attachments container can take up to 10 minutes to take effect; a 403 from blob storage inside that window is expected"
  log_info "next steps:"
  log_info "  1. bash scripts/azure/entra.sh          (app registrations, the runtime and migrator service principals and their client certificates)"
  log_info "  2. bash scripts/azure/provision.sh      (grants both service principals their roles)"
  log_info "  3. create Erp--Platform--Attachments--EncryptionKey in $ERP_AZURE_KEYVAULT_DEV_NAME and in $ERP_AZURE_KEYVAULT_PROD_NAME once each (docs/operations/runbooks/secrets.md, section 5f)"
  log_info "  4. bash scripts/azure/seed.sh           (configuration values and Key Vault references)"
  log_info "  5. $HOME_ADDRESS_HINT   (docs/operations/runbooks/home-address.md), then bash scripts/azure/database.sh   (docs/operations/runbooks/database.md)"
  log_info "  6. bash scripts/azure/verify.sh, then bash scripts/azure/verify.sh --entra and bash scripts/azure/verify.sh --database"
}

main "$@"
