#!/usr/bin/env bash

if [[ -n "${ERP_DEPLOY_COMMON_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_DEPLOY_COMMON_SH_LOADED=1

set -Eeuo pipefail
shopt -s inherit_errexit

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
readonly SCRIPT_DIR REPO_ROOT

readonly HOST_CONFIG_DIR="$REPO_ROOT/infra/host"
readonly DEPLOY_USER='erp-ai-pro'
readonly DEPLOY_HOME='/srv/erp-ai-pro'
readonly DEPLOY_CHECKOUT="$DEPLOY_HOME/ERP-AI-Pro"
readonly DEPLOY_COMPOSE_DIR="$DEPLOY_CHECKOUT/infra/compose"
readonly DEPLOY_LOCK="$DEPLOY_HOME/deploy.lock"
readonly STACK_VOLUMES=(erp-ai-pro-data-protection-keys erp-ai-pro-proxy-data)
readonly CLIENT_CERTIFICATES=(erp-runtime-client.pem erp-migrator-client.pem)
# The first gh release that compares --source-ref with regard to case and --signer-workflow whole, never as a prefix, which
# the release checks of deploy.sh rely on.
readonly MINIMUM_GH_VERSION='2.102.0'

DRY_RUN="${DRY_RUN:-0}"
ACCEPTS_DRY_RUN="${ACCEPTS_DRY_RUN:-1}"
USAGE="${USAGE:-}"
ARGS=()
CHANGES=()

log_step() { printf '\n==> %s\n' "$*" >&2; }
log_info() { printf '    %s\n' "$*" >&2; }
log_warn() { printf '[warn] %s\n' "$*" >&2; }
log_error() { printf '[error] %s\n' "$*" >&2; }

die() {
  log_error "$*"
  exit 1
}

require_command() {
  local name
  for name in "$@"; do
    command -v "$name" > /dev/null 2>&1 || die "required command '$name' is not on PATH"
  done
}

is_root() {
  (( EUID == 0 ))
}

quote_command() {
  local quoted
  printf -v quoted '%q ' "$@"
  printf '%s' "${quoted% }"
}

run() {
  if (( DRY_RUN )); then
    printf '[dry-run] %s\n' "$(quote_command "$@")" >&2
    return 0
  fi
  "$@"
}

record_change() {
  CHANGES+=("$*")
  if (( DRY_RUN )); then
    log_info "would change: $*"
  else
    log_info "changed: $*"
  fi
}

parse_args() {
  ARGS=()
  while (( $# > 0 )); do
    case "$1" in
      --dry-run)
        (( ACCEPTS_DRY_RUN )) || die "unknown option '$1'"
        DRY_RUN=1
        ;;
      --help | -h)
        printf '%s\n' "$USAGE"
        exit 0
        ;;
      --)
        shift
        ARGS+=("$@")
        break
        ;;
      -*)
        die "unknown option '$1' (see --help)"
        ;;
      *)
        ARGS+=("$1")
        ;;
    esac
    shift
  done
}

is_ipv4() {
  local address="$1" octet
  [[ "$address" =~ ^([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$ ]] || return 1
  for octet in "${BASH_REMATCH[@]:1}"; do
    [[ "$octet" =~ ^(0|[1-9][0-9]*)$ ]] && (( octet <= 255 )) || return 1
  done
}

ipv4_to_number() {
  local IFS=. a b c d
  read -r a b c d <<< "$1"
  printf '%s' "$(( (a << 24) | (b << 16) | (c << 8) | d ))"
}

number_to_ipv4() {
  local number="$1"
  printf '%s.%s.%s.%s' "$(( (number >> 24) & 255 ))" "$(( (number >> 16) & 255 ))" "$(( (number >> 8) & 255 ))" "$(( number & 255 ))"
}

ipv4_network_of() {
  local address="$1" prefix="$2" mask
  mask="$(( prefix == 0 ? 0 : (0xFFFFFFFF << (32 - prefix)) & 0xFFFFFFFF ))"
  printf '%s/%s' "$(number_to_ipv4 "$(( $(ipv4_to_number "$address") & mask ))")" "$prefix"
}

is_ipv4_network() {
  local network="$1"
  [[ "$network" == */* ]] || return 1
  local address="${network%/*}" prefix="${network#*/}"
  is_ipv4 "$address" && [[ "$prefix" =~ ^(0|[1-9][0-9]?)$ ]] && (( prefix <= 32 )) \
    && [[ "$(ipv4_network_of "$address" "$prefix")" == "$network" ]]
}

ipv4_in_network() {
  local address="$1" network="$2"
  local prefix="${network#*/}"
  [[ "$(ipv4_network_of "$address" "$prefix")" == "$network" ]]
}

mode_and_owner_of() {
  stat -c '%a %U:%G' -- "$1" 2> /dev/null
}

# Returns 0 when the target was (or, in a dry run, would be) written and 1 when it already matched the source.
install_config() {
  local source="$1" target="$2" mode="$3" owner="$4" description="$5"
  [[ -f "$source" ]] || die "$source is missing from this checkout"
  if [[ -e "$target" && ! -r "$target" ]]; then
    record_change "$description ($target; it could not be read without root, so a run with sudo compares it)"
    return 0
  fi
  if [[ -f "$target" ]] && cmp -s -- "$source" "$target" && [[ "$(mode_and_owner_of "$target")" == "$mode $owner" ]]; then
    log_info "in place: $description ($target)"
    return 1
  fi
  record_change "$description ($target)"
  # Callers test the result, which suspends errexit inside this function, so a failed write must stop the run itself.
  run install -D -m "$mode" -o "${owner%%:*}" -g "${owner##*:}" -- "$source" "$target" || die "cannot write $target"
  return 0
}

ensure_directory() {
  local path="$1" mode="$2" owner="$3" description="$4" current
  if current="$(mode_and_owner_of "$path")" && [[ -d "$path" ]]; then
    if [[ "$current" == "$mode $owner" ]]; then
      log_info "in place: $description ($path)"
      return 0
    fi
    record_change "$description: mode $mode and owner $owner instead of $current ($path)"
    run chmod -- "$mode" "$path" || die "cannot change the mode of $path"
    run chown -- "$owner" "$path" || die "cannot change the owner of $path"
    return 0
  fi
  record_change "$description ($path)"
  run install -d -m "$mode" -o "${owner%%:*}" -g "${owner##*:}" -- "$path" || die "cannot create $path"
}

key_fingerprints() {
  local file="$1" line
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ "$line" =~ ^[[:space:]]*(#|$) ]] && continue
    ssh-keygen -l -f /dev/stdin <<< "$line" | awk '{ print $2 }'
  done < "$file"
}

valid_key_count() {
  local file="$1" line count=0
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ "$line" =~ ^[[:space:]]*(#|$) ]] && continue
    ssh-keygen -l -f /dev/stdin <<< "$line" > /dev/null 2>&1 || return 1
    count=$((count + 1))
  done < "$file"
  printf '%s' "$count"
}
