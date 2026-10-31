#!/usr/bin/env bash

if [[ -n "${ERP_AZURE_RECORDS_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_AZURE_RECORDS_SH_LOADED=1

# shellcheck source=common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"

readonly RECORDS_SETTING_NAME='erp-ai-pro-records'
readonly MONITOR_API_VERSION='2021-05-01-preview'
readonly ENTRA_DIAGNOSTICS_API_VERSION='2017-04-01'
readonly ACTIVITY_LOG_CATEGORIES=(Administrative Security ServiceHealth Alert Recommendation Policy Autoscale ResourceHealth)
readonly ENTRA_LOG_CATEGORIES=(AuditLogs SignInLogs NonInteractiveUserSignInLogs ServicePrincipalSignInLogs ManagedIdentitySignInLogs)
readonly ENTRA_ROLE_HINT='the Microsoft Entra role Security Administrator or Global Administrator'

ENTRA_RECORDS_PROBLEM=''

logs_storage_account_id() {
  storage_account_resource_id "$ERP_AZURE_STORAGE_LOGS_NAME"
}

resource_records_url() {
  local resource_id="$1"
  printf '%s/providers/Microsoft.Insights/diagnosticSettings/%s?api-version=%s' "$resource_id" "$RECORDS_SETTING_NAME" "$MONITOR_API_VERSION"
}

activity_log_records_url() {
  resource_records_url "/subscriptions/$ERP_AZURE_SUBSCRIPTION_ID"
}

entra_records_url() {
  printf '/providers/microsoft.aadiam/diagnosticSettings/%s?api-version=%s' "$RECORDS_SETTING_NAME" "$ENTRA_DIAGNOSTICS_API_VERSION"
}

records_setting_json() {
  local url="$1"
  local output
  if output="$(az_read rest --method get --url "$url" --output json 2>&1)"; then
    printf '%s' "$output"
    return 0
  fi
  if [[ "$output" == *NotFound* || "$output" == *'Not Found'* ]]; then
    return 0
  fi
  printf '%s\n' "$output" >&2
  return 1
}

records_setting_problems() {
  local setting_json="$1" storage_account_id="$2"
  shift 2
  node -e '
let raw = "";
process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => {
  const properties = JSON.parse(raw).properties ?? {};
  const [account, ...expected] = process.argv.slice(1);
  const problems = [];
  if (String(properties.storageAccountId ?? "").toLowerCase() !== account.toLowerCase()) {
    problems.push(`it sends to ${properties.storageAccountId || "no storage account"}`);
  }
  for (const destination of ["workspaceId", "eventHubAuthorizationRuleId", "eventHubName", "serviceBusRuleId", "marketplacePartnerId"]) {
    if (properties[destination]) problems.push(`it also sends to ${properties[destination]}`);
  }
  const sent = (properties.logs ?? []).filter((log) => log.enabled === true).map((log) => log.category ?? `the category group ${log.categoryGroup}`);
  const missing = expected.filter((category) => !sent.some((name) => name.toLowerCase() === category.toLowerCase()));
  const extra = sent.filter((name) => !expected.some((category) => category.toLowerCase() === name.toLowerCase()));
  if (missing.length > 0) problems.push(`it leaves out ${missing.join(", ")}`);
  if (extra.length > 0) problems.push(`it also sends ${extra.join(", ")}`);
  const metrics = (properties.metrics ?? []).filter((metric) => metric.enabled === true).map((metric) => metric.category);
  if (metrics.length > 0) problems.push(`it sends the metrics ${metrics.join(", ")}`);
  process.stdout.write(problems.join("\n"));
});' "$storage_account_id" "$@" <<< "$setting_json"
}

records_sent_categories() {
  local setting_json="$1"
  node -e '
let raw = "";
process.stdin.on("data", (chunk) => { raw += chunk; }).on("end", () => {
  const logs = (JSON.parse(raw).properties ?? {}).logs ?? [];
  process.stdout.write(logs.filter((log) => log.enabled === true && log.category).map((log) => log.category).join("\n"));
});' <<< "$setting_json"
}

records_logs_json() {
  node -e 'process.stdout.write(JSON.stringify(process.argv.slice(1).map((category) => ({ category, enabled: true }))));' "$@"
}

records_entra_body() {
  local storage_account_id="$1"
  shift
  node -e 'const [storageAccountId, ...categories] = process.argv.slice(1); process.stdout.write(JSON.stringify({ properties: { storageAccountId, logs: categories.map((category) => ({ category, enabled: true })) } }));' \
    "$storage_account_id" "$@"
}

ensure_activity_log_records() {
  log_step "The subscription's activity log"
  local storage_account_id current problems
  storage_account_id="$(logs_storage_account_id)"
  current="$(records_setting_json "$(activity_log_records_url)")" \
    || die "cannot read the subscription's diagnostic setting $RECORDS_SETTING_NAME (see the error above)"
  if [[ -n "$current" ]]; then
    problems="$(records_setting_problems "$current" "$storage_account_id" "${ACTIVITY_LOG_CATEGORIES[@]}")"
    if [[ -z "$problems" ]]; then
      log_info "$RECORDS_SETTING_NAME sends ${ACTIVITY_LOG_CATEGORIES[*]} to $ERP_AZURE_STORAGE_LOGS_NAME; unchanged"
      return 0
    fi
    log_info "$RECORDS_SETTING_NAME differs (${problems//$'\n'/; }), so it is written again"
  else
    log_info "$RECORDS_SETTING_NAME does not exist yet"
  fi
  if (( DRY_RUN )); then
    run az monitor diagnostic-settings subscription create --name "$RECORDS_SETTING_NAME" --location "$ERP_AZURE_LOCATION" \
      --storage-account "$storage_account_id" --logs "@<file holding ${ACTIVITY_LOG_CATEGORIES[*]}, each enabled>" --output none --only-show-errors
    return 0
  fi
  local logs_file
  logs_file="$(azure_temp_file activity-log-categories)"
  records_logs_json "${ACTIVITY_LOG_CATEGORIES[@]}" > "$logs_file"
  if ! az monitor diagnostic-settings subscription create --name "$RECORDS_SETTING_NAME" --location "$ERP_AZURE_LOCATION" \
    --storage-account "$storage_account_id" --logs "@$logs_file" --output none --only-show-errors; then
    rm -f -- "$logs_file"
    die "cannot write the subscription's diagnostic setting $RECORDS_SETTING_NAME (see the error above)"
  fi
  rm -f -- "$logs_file"
  log_info "$RECORDS_SETTING_NAME written: ${ACTIVITY_LOG_CATEGORIES[*]} go to $ERP_AZURE_STORAGE_LOGS_NAME"
}

entra_records_refused() {
  local problem="$1"
  if (( DRY_RUN )); then
    log_warn "$problem; the real run needs the operator to hold $ENTRA_ROLE_HINT"
    return 0
  fi
  log_error "$problem"
  ENTRA_RECORDS_PROBLEM="$problem"
}

ensure_entra_records() {
  log_step "Microsoft Entra ID's audit and sign-in logs"
  local storage_account_id current problems
  storage_account_id="$(logs_storage_account_id)"
  if ! current="$(records_setting_json "$(entra_records_url)" 2>&1)"; then
    entra_records_refused "cannot read Microsoft Entra ID's diagnostic setting $RECORDS_SETTING_NAME: $(printf '%s' "$current" | sed -n '/[^[:space:]]/p' | tail -n 1)"
    current=''
    (( DRY_RUN )) || return 0
  fi
  if [[ -n "$current" ]]; then
    problems="$(records_setting_problems "$current" "$storage_account_id" "${ENTRA_LOG_CATEGORIES[@]}")"
    if [[ -z "$problems" ]]; then
      log_info "$RECORDS_SETTING_NAME sends ${ENTRA_LOG_CATEGORIES[*]} to $ERP_AZURE_STORAGE_LOGS_NAME; unchanged"
      return 0
    fi
    log_info "$RECORDS_SETTING_NAME differs (${problems//$'\n'/; }), so it is written again"
  fi
  if (( DRY_RUN )); then
    run az rest --method put --url "$(entra_records_url)" \
      --body "@<file holding the storage account $storage_account_id and ${ENTRA_LOG_CATEGORIES[*]}, each enabled>" --output none --only-show-errors
    return 0
  fi
  local body_file output
  body_file="$(azure_temp_file entra-records)"
  records_entra_body "$storage_account_id" "${ENTRA_LOG_CATEGORIES[@]}" > "$body_file"
  if ! output="$(az rest --method put --url "$(entra_records_url)" --body "@$body_file" --output none --only-show-errors 2>&1)"; then
    rm -f -- "$body_file"
    entra_records_refused "Microsoft Entra ID refused the diagnostic setting $RECORDS_SETTING_NAME with ${ENTRA_LOG_CATEGORIES[*]}: ${output//$'\n'/ }"
    return 0
  fi
  rm -f -- "$body_file"
  if ! current="$(records_setting_json "$(entra_records_url)" 2>&1)" || [[ -z "$current" ]]; then
    entra_records_refused "Microsoft Entra ID accepted the diagnostic setting $RECORDS_SETTING_NAME, which then could not be read back: ${current//$'\n'/ }"
    return 0
  fi
  local kept category accepted='' refused=''
  kept="$(records_sent_categories "$current")"
  kept=$'\n'"${kept,,}"$'\n'
  for category in "${ENTRA_LOG_CATEGORIES[@]}"; do
    if [[ "$kept" == *$'\n'"${category,,}"$'\n'* ]]; then
      accepted+="${accepted:+ }$category"
    else
      refused+="${refused:+ }$category"
    fi
  done
  log_info "$RECORDS_SETTING_NAME written: Microsoft Entra ID accepted ${accepted:-none of the categories}"
  if [[ -n "$refused" ]]; then
    entra_records_refused "Microsoft Entra ID kept the diagnostic setting $RECORDS_SETTING_NAME without $refused, which provision.sh requested"
  fi
}
