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
readonly BACKUP_CONTAINER_NAME='host-backups'
readonly BACKUP_JOURNAL_CONTAINER_NAME='host-journal'

storage_account_exists() {
  local account_name="$1"
  local output
  if output="$(az_read storage account show --name "$account_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query name --output tsv 2>&1)"; then
    return 0
  fi
  [[ "$output" == *ResourceNotFound* || "$output" == *'was not found'* ]] \
    || die "cannot read the storage account $account_name: $output"
  return 1
}

storage_ip_rules() {
  local account_name="$1"
  az_read storage account show --name "$account_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query 'networkRuleSet.ipRules[].ipAddressOrRange' --output tsv
}

storage_home_address() {
  local account_name="$1"
  local rules
  rules="$(storage_ip_rules "$account_name")" || die "cannot read the IP rules of $account_name"
  [[ -n "$rules" && "$rules" != *$'\n'* ]] || return 0
  printf '%s' "$rules"
}

storage_private_endpoint_connections() {
  local account_name="$1"
  az_read storage account show --name "$account_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query 'privateEndpointConnections[].[name, privateLinkServiceConnectionState.status]' --output tsv
}

storage_perimeter_count() {
  local account_name="$1"
  az_read rest --method get \
    --url "$(storage_account_resource_id "$account_name")/networkSecurityPerimeterConfigurations?api-version=$STORAGE_API_VERSION" \
    --query 'length(value)' --output tsv
}

private_endpoint_reject_hint() {
  local account_name="$1" names="$2"
  printf 'reject each of %s with: az storage account private-endpoint-connection reject --account-name %s --resource-group %s --name <connection>' \
    "$names" "$account_name" "$ERP_AZURE_RESOURCE_GROUP"
}

storage_expected_bypass() {
  local account_name="$1"
  if [[ "$account_name" == "$ERP_AZURE_STORAGE_LOGS_NAME" ]]; then
    printf 'AzureServices'
  else
    printf 'None'
  fi
}

storage_admitted_description() {
  local account_name="$1"
  if [[ "$(storage_expected_bypass "$account_name")" == AzureServices ]]; then
    printf "the home's address and trusted Microsoft services alone"
  else
    printf "the home's address alone"
  fi
}

check_storage_network_rules() {
  local account_name="$1"
  local settings rules connections perimeters expected_bypass admitted
  expected_bypass="$(storage_expected_bypass "$account_name")"
  admitted="$(storage_admitted_description "$account_name")"
  settings="$(az_read storage account show --name "$account_name" --resource-group "$ERP_AZURE_RESOURCE_GROUP" \
    --query '[publicNetworkAccess, networkRuleSet.defaultAction, networkRuleSet.bypass, length(networkRuleSet.virtualNetworkRules || `[]`), length(networkRuleSet.resourceAccessRules || `[]`), length(networkRuleSet.ipv6Rules || `[]`)]' \
    --output tsv)" || die "cannot read the network rules of $account_name"
  rules="$(storage_ip_rules "$account_name")" || die "cannot read the IP rules of $account_name"
  connections="$(storage_private_endpoint_connections "$account_name")" || die "cannot read the private endpoint connections of $account_name"
  perimeters="$(storage_perimeter_count "$account_name")" || die "cannot read the network security perimeters of $account_name"
  local public_access default_action bypass virtual_network_rules resource_rules ipv6_rules
  { read -r public_access; read -r default_action; read -r bypass; read -r virtual_network_rules; read -r resource_rules; read -r ipv6_rules; } <<< "$settings"
  local -a template_problems=() other_problems=()
  [[ "$public_access" == Enabled ]] || template_problems+=("its public network access is $public_access, not Enabled, so its own rules do not decide who reaches it")
  [[ "$default_action" == Deny ]] || template_problems+=("its default action is $default_action, not Deny")
  if [[ "$bypass" != "$expected_bypass" ]]; then
    if [[ "$expected_bypass" == None ]]; then
      template_problems+=("it lets $bypass past its rules")
    else
      template_problems+=("it lets $bypass past its rules, not the trusted Microsoft services that deliver its records")
    fi
  fi
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
  [[ -z "$approved_names" ]] || other_problems+=("it has approved private endpoint connections; $(private_endpoint_reject_hint "$account_name" "$approved_names")")
  (( perimeters == 0 )) || other_problems+=("it is associated with $perimeters network security perimeter profile(s), whose rules add to or replace its own; remove the account on the perimeter's Associated resources page in the Azure portal")
  if [[ -n "$pending_names" ]]; then
    log_warn "the storage account $account_name has pending private endpoint connection requests, which admit nobody until approved; $(private_endpoint_reject_hint "$account_name" "$pending_names")"
  fi
  (( ${#template_problems[@]} + ${#other_problems[@]} == 0 )) && return 0
  local problem
  for problem in "${template_problems[@]}" "${other_problems[@]}"; do
    log_warn "the storage account $account_name does not admit $admitted: $problem"
  done
  if (( ${#template_problems[@]} > 0 )); then
    log_info "bash scripts/azure/provision.sh converges the account's own settings to the template, the address of the database's rule $SQL_FIREWALL_RULE_NAME alone (its what-if lists the change first)"
  fi
  die "the storage account $account_name does not admit $admitted; resolve what is listed above"
}

ensure_storage_home_address() {
  local account_name="$1" address="$2"
  log_step "IP rule of the storage account $account_name"
  local current expected_bypass bypass_description
  current="$(storage_home_address "$account_name")"
  if [[ "$current" == "$address" ]]; then
    log_info "already admits $address alone"
    return 0
  fi
  if [[ -n "$current" ]]; then
    log_warn "moving the rule from $current onto $address alone: from then on only a request from $address reaches the files of $account_name"
  else
    log_info "admitting $address"
  fi
  expected_bypass="$(storage_expected_bypass "$account_name")"
  bypass_description='no bypass'
  [[ "$expected_bypass" == None ]] || bypass_description='trusted Microsoft services past the rules'
  local url
  url="$(storage_account_resource_id "$account_name")?api-version=$STORAGE_API_VERSION"
  if (( DRY_RUN )); then
    run az rest --method patch --url "$url" \
      --body "@<file holding networkAcls: default action Deny, $bypass_description, the IP rule $address alone, no IPv6, virtual network or resource instance rule>" \
      --output none --only-show-errors
    return 0
  fi
  local body_file
  body_file="$(azure_temp_file storage-network-rules)"
  printf '{"properties":{"networkAcls":{"bypass":"%s","defaultAction":"Deny","ipRules":[{"value":"%s","action":"Allow"}],"ipv6Rules":[],"virtualNetworkRules":[],"resourceAccessRules":[]}}}' \
    "$expected_bypass" "$address" > "$body_file"
  if ! az rest --method patch --url "$url" --body "@$body_file" --output none --only-show-errors; then
    rm -f -- "$body_file"
    die "cannot set the IP rule of $account_name (see the error above)"
  fi
  rm -f -- "$body_file"
  log_info "a changed rule can take a few minutes to apply"
}

container_immutability_state() {
  local account_name="$1" container_name="$2"
  local container_json
  if ! container_json="$(az_read rest --method get \
    --url "$(storage_container_resource_id "$account_name" "$container_name")?api-version=$STORAGE_API_VERSION" \
    --output json 2>&1)"; then
    [[ "$container_json" == *ContainerNotFound* || "$container_json" == *ResourceNotFound* || "$container_json" == *'was not found'* ]] \
      || die "cannot read the container $container_name of $account_name: $container_json"
    return 0
  fi
  [[ "$(json_field "$container_json" properties.hasImmutabilityPolicy)" == true ]] || return 0
  json_field "$container_json" properties.immutabilityPolicy.properties.state
}
