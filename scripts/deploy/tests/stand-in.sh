#!/usr/bin/env bash

set -euo pipefail

COMMAND="${0##*/}"
STATE="${0%/*}/../state"
CALLS="$STATE/calls"
REGISTRY='ghcr.io/dewiride-open-source/erp-ai-pro'
DOWNLOADS='https://github.com/Dewiride-Open-Source/ERP-AI-Pro/releases/download/'
RELEASE='none'

record() {
  printf '%s\n' "$*" >> "$CALLS"
}

unexpected() {
  record "UNEXPECTED $COMMAND $*"
  printf 'stand-in %s: unexpected call: %s\n' "$COMMAND" "$*" >&2
  exit 99
}

setting() {
  REPLY="$2"
  if [[ -f "$STATE/$1" ]]; then
    IFS= read -r REPLY < "$STATE/$1" || true
  fi
}

read_release() {
  local file line
  for file in "$@"; do
    while IFS= read -r line || [[ -n "$line" ]]; do
      if [[ "$line" == IMAGE_TAG=* ]]; then
        RELEASE="${line#IMAGE_TAG=}"
      fi
    done < "$file"
  done
}

docker_image() {
  record "docker image $*"
  local reference="${*: -1}" image
  case "${1:-}" in
    ls)
      image="${reference##*/}"
      if [[ -f "$STATE/docker/images/$image" ]]; then
        cat -- "$STATE/docker/images/$image"
      fi
      ;;
    inspect)
      image="${reference%@*}"
      image="${image##*/}"
      [[ -f "$STATE/docker/images/$image" ]] && grep -qxF -- "${reference##*@}" "$STATE/docker/images/$image"
      ;;
    rm) ;;
    *) unexpected image "$@" ;;
  esac
}

docker_compose() {
  local -a shown=(docker compose) env_files=()
  while (( $# > 0 )); do
    case "$1" in
      -f)
        shown+=(-f "$2")
        shift 2
        ;;
      --env-file)
        env_files+=("$2")
        shown+=(--env-file "${2##*/}")
        shift 2
        ;;
      *) break ;;
    esac
  done
  read_release "${env_files[@]}"
  record "${shown[*]} $* # $RELEASE"
  local folder="docker/$RELEASE" status
  case "${1:-} ${*: -1}" in
    'pull proxy')
      setting "$folder/pull-exit" 0
      return "$REPLY"
      ;;
    'run status')
      setting "$folder/status-exit" 0
      return "$REPLY"
      ;;
    'run migrate')
      setting "$folder/migrate-exit" 0
      return "$REPLY"
      ;;
    'up proxy')
      setting "$folder/up-exit" 0
      return "$REPLY"
      ;;
    'exec http://api:8080/healthz/ready')
      setting "$folder/ready-exit" 0
      status="$REPLY"
      setting "$folder/ready" Healthy
      if (( status == 0 )); then
        printf '%s' "$REPLY"
      fi
      return "$status"
      ;;
    *) unexpected compose "$@" ;;
  esac
}

stand_in_docker() {
  case "${1:-}" in
    compose) docker_compose "${@:2}" ;;
    image) docker_image "${@:2}" ;;
    *) unexpected "$@" ;;
  esac
}

stand_in_gh() {
  if [[ "$*" == --version ]]; then
    record "gh --version"
    setting gh/version 2.102.0
    printf 'gh version %s (2026-09-30)\nhttps://github.com/cli/cli/releases/tag/v%s\n' "$REPLY" "$REPLY"
    return 0
  fi
  [[ "${1:-} ${2:-}" == 'attestation verify' ]] || unexpected "$@"
  local -a shown=()
  local argument previous='' bundle='' predicate='' source_ref='' image kind content=''
  for argument in "$@"; do
    case "$previous" in
      --bundle)
        bundle="$argument"
        shown+=("${argument##*/}")
        ;;
      --predicate-type)
        predicate="$argument"
        shown+=("$argument")
        ;;
      --source-ref)
        source_ref="$argument"
        shown+=("$argument")
        ;;
      *) shown+=("$argument") ;;
    esac
    previous="$argument"
  done
  record "gh ${shown[*]}"
  image="${3#"oci://$REGISTRY/"}"
  image="${image%@*}"
  case "$predicate" in
    https://slsa.dev/provenance/v1) kind=provenance ;;
    https://spdx.dev/Document/v2.3) kind=sbom ;;
    *) unexpected "$@" ;;
  esac
  IFS= read -r content < "$bundle" || true
  if [[ "$content" != "$image $kind ${source_ref#refs/tags/}" ]]; then
    printf 'Error: the bundle %s holds no %s of %s\n' "$bundle" "$kind" "$image" >&2
    return 1
  fi
  setting "gh/$image.$kind" accept
  case "$REPLY" in
    accept) printf '1377214678\n' ;;
    refuse)
      printf 'Error: verifying with issuer "sigstore.dev": failed to verify certificate identity\n' >&2
      return 1
      ;;
    repository:*) printf '%s\n' "${REPLY#repository:}" ;;
    *) unexpected "$@" ;;
  esac
}

stand_in_curl() {
  local -a shown=()
  local argument previous='' output='' url=''
  for argument in "$@"; do
    if [[ "$previous" == --output ]]; then
      output="$argument"
      shown+=("${argument##*/}")
    else
      shown+=("$argument")
    fi
    if [[ "$argument" == https://* ]]; then
      url="$argument"
    fi
    previous="$argument"
  done
  record "curl ${shown[*]}"
  [[ -n "$output" && "$url" == "$DOWNLOADS"* ]] || unexpected "$@"
  if [[ ! -f "$STATE/release/${url#"$DOWNLOADS"}" ]]; then
    printf 'curl: (22) The requested URL returned error: 404\n' >&2
    return 22
  fi
  cp -- "$STATE/release/${url#"$DOWNLOADS"}" "$output"
}

install_tree() {
  local tree="$STATE/git/trees/$1" checkout="$2" file relative
  if [[ ! -d "$tree" ]]; then
    return 0
  fi
  while IFS= read -r -d '' file; do
    relative="${file#"$tree"/}"
    cp -- "$file" "$checkout/$relative.stand-in"
    mv -f -- "$checkout/$relative.stand-in" "$checkout/$relative"
  done < <(find "$tree" -type f -print0)
}

stand_in_git() {
  local checkout='' tag
  while (( $# > 0 )); do
    case "$1" in
      -C)
        checkout="$2"
        shift 2
        ;;
      -c) shift 2 ;;
      *) break ;;
    esac
  done
  record "git $*"
  [[ -n "$checkout" ]] || unexpected "$@"
  case "${1:-} ${*: -1}" in
    'fetch origin')
      setting git/fetch-exit 0
      return "$REPLY"
      ;;
    'rev-parse HEAD')
      setting git/head ''
      printf '%s\n' "$REPLY"
      ;;
    'rev-parse refs/tags/'*'^{commit}')
      tag="${*: -1}"
      tag="${tag#refs/tags/}"
      tag="${tag%'^{commit}'}"
      [[ -f "$STATE/git/tags/$tag" ]] || return 1
      setting "git/tags/$tag" ''
      printf '%s\n' "$REPLY"
      ;;
    'merge-base refs/remotes/origin/main')
      [[ "${2:-}" == --is-ancestor ]] || unexpected "$@"
      grep -qxF -- "$3" "$STATE/git/main"
      ;;
    'status --untracked-files=no')
      if [[ -f "$STATE/git/dirty" ]]; then
        cat -- "$STATE/git/dirty"
      fi
      ;;
    'cat-file '*':scripts/deploy/deploy.sh') [[ ! -f "$STATE/git/no-deploy-script" ]] ;;
    'diff scripts/deploy/lib/common.sh')
      if [[ -f "$STATE/git/host-changes" ]]; then
        cat -- "$STATE/git/host-changes"
      fi
      ;;
    'checkout '*)
      printf '%s\n' "${*: -1}" > "$STATE/git/head"
      install_tree "${*: -1}" "$checkout"
      ;;
    *) unexpected "$@" ;;
  esac
}

stand_in_mv() {
  setting mv-refuses ''
  if [[ -n "$REPLY" && "${*: -1}" == *"$REPLY" ]]; then
    record "mv refused ${*: -1}"
    printf 'mv: cannot move to %s: No space left on device\n' "${*: -1}" >&2
    return 1
  fi
  PATH="${PATH#"${0%/*}:"}" mv "$@"
}

case "$COMMAND" in
  docker) stand_in_docker "$@" ;;
  gh) stand_in_gh "$@" ;;
  curl) stand_in_curl "$@" ;;
  git) stand_in_git "$@" ;;
  mv) stand_in_mv "$@" ;;
  flock)
    record "flock $*"
    [[ ! -f "$STATE/flock-held" || ( "$1" == --wait && -f "$STATE/flock-released" ) ]]
    ;;
  id)
    [[ "$*" == -un ]] || unexpected "$@"
    record "id -un"
    setting account erp-ai-pro
    printf '%s\n' "$REPLY"
    ;;
  date)
    [[ "$*" == +%H%M ]] || unexpected "$@"
    record "date +%H%M"
    setting clock 1200
    printf '%s\n' "$REPLY"
    ;;
  sleep) record "sleep $*" ;;
  *) unexpected "$@" ;;
esac
