#!/usr/bin/env bash

USAGE='Usage: bash scripts/azure/check.sh

Compiles the Bicep template with linter rules as errors and syntax-checks every
shell script under scripts/azure. Needs the Azure CLI with Bicep; no sign-in.

Options:
  --help            Show this help.'

ACCEPTS_DRY_RUN=0
ACCEPTS_PARAMS=0

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/lib/common.sh"

readonly MINIMUM_BICEP_MAJOR=0
readonly MINIMUM_BICEP_MINOR=30
readonly TEMPLATE_FILE='scripts/azure/bicep/main.bicep'
SHELL_SCRIPT_COUNT=0

bicep_version() {
  { az bicep version 2> /dev/null || true; } | tr -d '\r' | sed -n 's/.*version \([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\).*/\1/p' | head -n 1
}

ensure_bicep() {
  local version
  version="$(bicep_version)"
  if [[ -z "$version" ]]; then
    log_info "Bicep CLI not found; installing through az bicep install"
    az bicep install --only-show-errors
    version="$(bicep_version)"
    [[ -n "$version" ]] || die "Bicep CLI still unavailable after az bicep install; run 'az bicep install' by hand"
  fi
  local major minor
  major="${version%%.*}"
  minor="${version#*.}"
  minor="${minor%%.*}"
  if (( major < MINIMUM_BICEP_MAJOR || (major == MINIMUM_BICEP_MAJOR && minor < MINIMUM_BICEP_MINOR) )); then
    die "Bicep CLI $version is older than $MINIMUM_BICEP_MAJOR.$MINIMUM_BICEP_MINOR; run: az bicep upgrade"
  fi
  log_info "Bicep CLI $version"
}

check_template() {
  log_step "bicep build $TEMPLATE_FILE"
  [[ -f "$TEMPLATE_FILE" ]] || die "template '$TEMPLATE_FILE' not found"
  az bicep build --file "$TEMPLATE_FILE" --stdout > /dev/null
  log_info "template compiles with linter rules as errors"
}

check_shell_syntax() {
  log_step "bash -n"
  local script
  local -a scripts=()
  while IFS= read -r script; do
    scripts+=("$script")
  done < <(find scripts/azure -type f -name '*.sh' | sort)
  (( ${#scripts[@]} > 0 )) || die "no shell scripts found under scripts/azure"
  for script in "${scripts[@]}"; do
    bash -n "$script"
    log_info "$script"
  done
  if command -v shellcheck > /dev/null 2>&1; then
    log_step "shellcheck"
    shellcheck --external-sources --source-path=SCRIPTDIR --shell=bash --severity=warning scripts/azure/check.sh scripts/azure/provision.sh scripts/azure/verify.sh scripts/azure/entra.sh scripts/azure/export-runtime-certificate.sh
    log_info "clean"
  else
    log_info "shellcheck not on PATH; skipped"
  fi
  SHELL_SCRIPT_COUNT="${#scripts[@]}"
}

main() {
  parse_args "$@"
  (( ${#ARGS[@]} == 0 )) || die "unexpected argument '${ARGS[0]}'"
  cd -- "$REPO_ROOT" || die "cannot change to $REPO_ROOT"
  command -v az > /dev/null 2>&1 || die "Azure CLI 'az' is not on PATH; install it from https://learn.microsoft.com/cli/azure/install-azure-cli and run 'az bicep install'"
  require_command bash sed find sort

  log_step "Bicep CLI"
  ensure_bicep
  check_template
  check_shell_syntax
  printf 'azure scripts check ok: bicep template compiles, %s shell script(s) parse\n' "$SHELL_SCRIPT_COUNT"
}

main "$@"
