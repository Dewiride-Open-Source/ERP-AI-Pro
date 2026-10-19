#!/usr/bin/env bash

if [[ -n "${ERP_AZURE_STORAGE_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_AZURE_STORAGE_SH_LOADED=1

# shellcheck source=common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"
# shellcheck source=sql.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/sql.sh"

readonly STORAGE_API_VERSION='2026-04-01'

production_storage_exists() {
  local output
  if output="$(az_read storage account show --name "$ERP_AZURE_STORAGE_PROD_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query name --output tsv 2>&1)"; then
    return 0
  fi
  [[ "$output" == *ResourceNotFound* || "$output" == *'was not found'* ]] \
    || die "cannot read the storage account $ERP_AZURE_STORAGE_PROD_NAME: $output"
  return 1
}

production_storage_ip_rules() {
  az_read storage account show --name "$ERP_AZURE_STORAGE_PROD_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query 'networkRuleSet.ipRules[].ipAddressOrRange' --output tsv
}

production_storage_home_address() {
  local rules
  rules="$(production_storage_ip_rules)" || die "cannot read the IP rules of $ERP_AZURE_STORAGE_PROD_NAME"
  [[ -n "$rules" && "$rules" != *$'\n'* ]] || return 0
  printf '%s' "$rules"
}

production_storage_private_endpoint_connections() {
  az_read storage account show --name "$ERP_AZURE_STORAGE_PROD_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query 'privateEndpointConnections[].[name, privateLinkServiceConnectionState.status]' --output tsv
}

production_storage_perimeter_count() {
  az_read rest --method get \
    --url "$(storage_account_resource_id "$ERP_AZURE_STORAGE_PROD_NAME")/networkSecurityPerimeterConfigurations?api-version=$STORAGE_API_VERSION" \
    --query 'length(value)' --output tsv
}

private_endpoint_reject_hint() {
  local names="$1"
  printf 'reject each of %s with: az storage account private-endpoint-connection reject --account-name %s --resource-group %s --name <connection>' \
    "$names" "$ERP_AZURE_STORAGE_PROD_NAME" "$ERP_AZURE_RESOURCE_GROUP"
}

check_production_storage_network_rules() {
  local settings rules connections perimeters
  settings="$(az_read storage account show --name "$ERP_AZURE_STORAGE_PROD_NAME" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query '[publicNetworkAccess, networkRuleSet.defaultAction, networkRuleSet.bypass, length(networkRuleSet.virtualNetworkRules || `[]`), length(networkRuleSet.resourceAccessRules || `[]`), length(networkRuleSet.ipv6Rules || `[]`)]' \
    --output tsv)" || die "cannot read the network rules of $ERP_AZURE_STORAGE_PROD_NAME"
  rules="$(production_storage_ip_rules)" || die "cannot read the IP rules of $ERP_AZURE_STORAGE_PROD_NAME"
  connections="$(production_storage_private_endpoint_connections)" || die "cannot read the private endpoint connections of $ERP_AZURE_STORAGE_PROD_NAME"
  perimeters="$(production_storage_perimeter_count)" || die "cannot read the network security perimeters of $ERP_AZURE_STORAGE_PROD_NAME"
  local public_access default_action bypass virtual_network_rules resource_rules ipv6_rules
  { read -r public_access; read -r default_action; read -r bypass; read -r virtual_network_rules; read -r resource_rules; read -r ipv6_rules; } <<< "$settings"
  local -a template_problems=() other_problems=()
  [[ "$public_access" == Enabled ]] || template_problems+=("its public network access is $public_access, not Enabled, so its own rules do not decide who reaches it")
  [[ "$default_action" == Deny ]] || template_problems+=("its default action is $default_action, not Deny")
  [[ "$bypass" == None ]] || template_problems+=("it lets $bypass past its rules")
  (( virtual_network_rules == 0 )) || template_problems+=("it has $virtual_network_rules virtual network rule(s)")
  (( resource_rules == 0 )) || template_problems+=("it has $resource_rules resource instance rule(s)")
  (( ipv6_rules == 0 )) || template_problems+=("it has $ipv6_rules IPv6 rule(s)")
  local rule count=0
  while IFS= read -r rule; do
    [[ -n "$rule" ]] || continue
    count=$((count + 1))
    is_public_ipv4 "$rule" || template_problems+=("its IP rule $rule is not one public IPv4 address")
  done <<< "$rules"
  (( count <= 1 )) || template_problems+=("it has $count IP rules")
  local name status approved_names='' pending_names=''
  while IFS=$'\t' read -r name status; do
    case "$status" in
      Approved) approved_names+="${approved_names:+ }$name" ;;
      Pending) pending_names+="${pending_names:+ }$name" ;;
    esac
  done <<< "$connections"
  [[ -z "$approved_names" ]] || other_problems+=("it has approved private endpoint connections; $(private_endpoint_reject_hint "$approved_names")")
  (( perimeters == 0 )) || other_problems+=("it is associated with $perimeters network security perimeter profile(s), whose rules add to or replace its own; remove the account on the perimeter's Associated resources page in the Azure portal")
  if [[ -n "$pending_names" ]]; then
    log_warn "the storage account $ERP_AZURE_STORAGE_PROD_NAME has pending private endpoint connection requests, which admit nobody until approved; $(private_endpoint_reject_hint "$pending_names")"
  fi
  (( ${#template_problems[@]} + ${#other_problems[@]} == 0 )) && return 0
  local problem
  for problem in "${template_problems[@]}" "${other_problems[@]}"; do
    log_warn "the storage account $ERP_AZURE_STORAGE_PROD_NAME does not admit the home's address alone: $problem"
  done
  if (( ${#template_problems[@]} > 0 )); then
    log_info "bash scripts/azure/provision.sh converges the account's own settings to the template, the address of the database's rule $SQL_FIREWALL_RULE_NAME alone (its what-if lists the change first)"
  fi
  die "the storage account $ERP_AZURE_STORAGE_PROD_NAME does not admit the home's address alone; resolve what is listed above"
}

ensure_production_storage_home_address() {
  local address="$1"
  log_step "IP rule of the storage account $ERP_AZURE_STORAGE_PROD_NAME"
  local current
  current="$(production_storage_home_address)"
  if [[ "$current" == "$address" ]]; then
    log_info "already admits $address alone"
    return 0
  fi
  if [[ -n "$current" ]]; then
    log_warn "moving the rule from $current onto $address alone: the server at home reaches the attachments only when $address is the home's address"
  else
    log_info "admitting $address"
  fi
  local url
  url="$(storage_account_resource_id "$ERP_AZURE_STORAGE_PROD_NAME")?api-version=$STORAGE_API_VERSION"
  if (( DRY_RUN )); then
    run az rest --method patch --url "$url" \
      --body "@<file holding networkAcls: default action Deny, no bypass, the IP rule $address alone, no IPv6, virtual network or resource instance rule>" \
      --output none --only-show-errors
    return 0
  fi
  local body_file
  body_file="$(azure_temp_file storage-network-rules)"
  printf '{"properties":{"networkAcls":{"bypass":"None","defaultAction":"Deny","ipRules":[{"value":"%s","action":"Allow"}],"ipv6Rules":[],"virtualNetworkRules":[],"resourceAccessRules":[]}}}' \
    "$address" > "$body_file"
  if ! az rest --method patch --url "$url" --body "@$body_file" --output none --only-show-errors; then
    rm -f -- "$body_file"
    die "cannot set the IP rule of $ERP_AZURE_STORAGE_PROD_NAME (see the error above)"
  fi
  rm -f -- "$body_file"
  log_info "a changed rule can take a few minutes to apply"
}
