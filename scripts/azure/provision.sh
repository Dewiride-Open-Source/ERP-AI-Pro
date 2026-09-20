#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/provision.sh [--dry-run] [--params <file>]

Creates or converges the ERP-AI-Pro resource group, the App Configuration store,
both Key Vaults with their data-protection keys, the role assignments and the
labelled Erp:Sentinel keys. Safe to run repeatedly; never deletes or purges.

Options:
  --dry-run         Print the what-if result and every command that would run; write nothing.
  --params <file>   Parameter file (default: scripts/azure/params.env).
  --help            Show this help.'

source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/appconfig.sh"

readonly SENTINEL_KEY='Erp:Sentinel'
readonly LABELS=(local-dev production)
readonly TEMPLATE_FILE='scripts/azure/bicep/main.bicep'

deployment_output() {
  local outputs_json="$1" name="$2"
  local value
  value="$(json_field "$outputs_json" "$name.value")"
  [[ -n "$value" ]] || die "deployment output '$name' missing"
  printf '%s' "$value"
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

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  load_params
  cd -- "$REPO_ROOT"
  require_command az node
  [[ -f "$TEMPLATE_FILE" ]] || die "template '$TEMPLATE_FILE' not found"

  log_step "Checking the Azure CLI sign-in"
  ensure_login

  log_step "Resolving principals"
  local operator_id developers_group_id runtime_id
  operator_id="$(signed_in_user_object_id)"
  log_info "operator: $operator_id"
  developers_group_id="$(developers_group_object_id)"
  if [[ -n "$developers_group_id" ]]; then
    log_info "developers group '$ERP_AZURE_DEVELOPERS_GROUP': $developers_group_id"
  else
    log_info "developers group: not configured; developer roles skipped"
  fi
  runtime_id="$(runtime_service_principal_object_id)"
  if [[ -n "$runtime_id" ]]; then
    log_info "runtime service principal '$ERP_AZURE_APP_RUNTIME_NAME': $runtime_id"
  else
    log_info "runtime service principal '$ERP_AZURE_APP_RUNTIME_NAME' not found; runtime roles skipped until entra.sh has run"
  fi

  local -a template_parameters=(
    "location=$ERP_AZURE_LOCATION"
    "configurationStoreName=$ERP_AZURE_APPCONFIG_NAME"
    "configurationStoreSku=$ERP_AZURE_APPCONFIG_SKU"
    "developmentKeyVaultName=$ERP_AZURE_KEYVAULT_DEV_NAME"
    "productionKeyVaultName=$ERP_AZURE_KEYVAULT_PROD_NAME"
    "operatorPrincipalId=$operator_id"
    "developersGroupPrincipalId=$developers_group_id"
    "runtimePrincipalId=$runtime_id"
  )

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

  log_step "Deployment preview (what-if)"
  if [[ "$group_exists" == "true" ]] || (( ! DRY_RUN )); then
    az deployment group what-if --resource-group "$ERP_AZURE_RESOURCE_GROUP" --mode Incremental \
      --template-file "$TEMPLATE_FILE" --parameters "${template_parameters[@]}" --only-show-errors
  else
    log_info "what-if skipped until the resource group exists"
  fi

  local deployment_name="erp-ai-pro-$(date -u +%Y%m%d%H%M%S)"
  local store_endpoint="https://$ERP_AZURE_APPCONFIG_NAME.azconfig.io"
  local development_vault_uri="https://$ERP_AZURE_KEYVAULT_DEV_NAME.vault.azure.net/"
  local production_vault_uri="https://$ERP_AZURE_KEYVAULT_PROD_NAME.vault.azure.net/"

  log_step "Deployment $deployment_name"
  if (( DRY_RUN )); then
    run az deployment group create --name "$deployment_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
      --mode Incremental --template-file "$TEMPLATE_FILE" --parameters "${template_parameters[@]}" \
      --query properties.outputs --output json --only-show-errors
    log_step "Data plane"
    local label
    for label in "${LABELS[@]}"; do
      run az appconfig kv set --name "$ERP_AZURE_APPCONFIG_NAME" --auth-mode login --key "$SENTINEL_KEY" --label "$label" \
        --value "$(utc_timestamp)" --yes --output none --only-show-errors
    done
    log_info "each sentinel is written only when absent; an existing value is never bumped"
  else
    local outputs_json
    outputs_json="$(az_read deployment group create --name "$deployment_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
      --mode Incremental --template-file "$TEMPLATE_FILE" --parameters "${template_parameters[@]}" \
      --query properties.outputs --output json)"
    store_endpoint="$(deployment_output "$outputs_json" configurationStoreEndpoint)"
    development_vault_uri="$(deployment_output "$outputs_json" developmentKeyVaultUri)"
    production_vault_uri="$(deployment_output "$outputs_json" productionKeyVaultUri)"
    log_info "deployed"

    log_step "Data plane"
    local label
    for label in "${LABELS[@]}"; do
      ensure_sentinel "$label"
    done
  fi

  log_step "Summary"
  printf 'APPCONFIG_ENDPOINT=%s\n' "$store_endpoint"
  printf 'KEYVAULT_DEV_URI=%s\n' "$development_vault_uri"
  printf 'KEYVAULT_PROD_URI=%s\n' "$production_vault_uri"
  if (( DRY_RUN )); then
    log_info "dry run: nothing was created or changed"
  fi
  log_info "next steps:"
  log_info "  1. bash scripts/azure/entra.sh          (app registrations and the runtime service principal)"
  log_info "  2. bash scripts/azure/provision.sh      (grants the runtime service principal its roles)"
  log_info "  3. bash scripts/azure/seed.sh           (configuration values and Key Vault references)"
  log_info "  4. bash scripts/azure/verify.sh"
}

main "$@"
