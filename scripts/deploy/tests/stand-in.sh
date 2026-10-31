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

real() {
  PATH="${PATH#"${0%/*}:"}" "$@"
}

volume_of_service() {
  case "$1" in
    key-ring-*) REPLY=erp-ai-pro-data-protection-keys ;;
    proxy-data-*) REPLY=erp-ai-pro-proxy-data ;;
    *) return 1 ;;
  esac
}

owner_of_service() {
  case "$1" in
    key-ring-*) REPLY=1654:1654 ;;
    proxy-data-*) REPLY=10001:10001 ;;
  esac
}

list_volume() {
  local folder="$1" path
  local -a depth=()
  if [[ -n "${2:-}" ]]; then
    depth=(-maxdepth "$2")
  fi
  while IFS= read -r path; do
    printf '/volume%s\n' "${path#"$folder"}"
  done < <(find "$folder" -mindepth 1 "${depth[@]}" | sort)
}

volume_command() {
  local service="$1" role="${1##*-}" volume folder top owner
  shift
  volume_of_service "$service" || unexpected "$service" "$@"
  volume="$REPLY"
  folder="$STATE/volumes/$volume"
  setting "volumes/$volume.top" '0:0 755'
  top="$REPLY"
  owner_of_service "$service"
  owner="$REPLY"
  setting "docker/backup/$service-exit" 0
  if (( REPLY != 0 )); then
    printf 'stand-in %s: fails as the scenario says\n' "$service" >&2
    return "$REPLY"
  fi
  case "$role $*" in
    'reader tar -cf - -C /volume .') real tar -cf - -C "$folder" . ;;
    'reader stat -c %u:%g %a /volume') printf '%s\n' "$top" ;;
    'reader find /volume -mindepth 1 -maxdepth 1') list_volume "$folder" 1 ;;
    'reader find /volume -mindepth 1') list_volume "$folder" ;;
    'writer find /volume -mindepth 1 -delete' | 'writer tar -xof - -C /volume')
      if [[ "${top%% *}" != "$owner" ]]; then
        printf '%s: can'"'"'t write in /volume: Permission denied\n' "$1" >&2
        return 1
      fi
      if [[ "$1" == find ]]; then
        find "$folder" -mindepth 1 -delete
      elif [[ -e "$STATE/docker/backup/$service-extracts-nothing" ]]; then
        cat > /dev/null
      else
        real tar -xof - -C "$folder"
      fi
      ;;
    'owner chown '*' /volume') printf '%s %s\n' "$2" "${top#* }" > "$STATE/volumes/$volume.top" ;;
    'owner chmod 700 /volume')
      if [[ ! -e "$STATE/docker/backup/$service-chmod-changes-nothing" ]]; then
        printf '%s 700\n' "${top%% *}" > "$STATE/volumes/$volume.top"
      fi
      ;;
    *) unexpected "$service" "$@" ;;
  esac
}

blob_command() {
  local source="$1" target="$2" client="$3" tenant="$4"
  shift 4
  [[ "$client" =~ ^[0-9a-f-]{36}$ && "$tenant" =~ ^[0-9a-f-]{36}$ && "${1:-} ${2:-}" == 'storage blob' ]] || unexpected azure-cli "$@"
  local action="${3:-}" argument previous='' name='' file='' container='' blob created
  for argument in "${@:4}"; do
    case "$previous" in
      --name) name="$argument" ;;
      --file) file="$argument" ;;
      --container-name) container="$argument" ;;
    esac
    previous="$argument"
  done
  [[ -n "$container" ]] || unexpected azure-cli "$@"
  blob="$STATE/blobs/$container/$name"
  created="$STATE/blobs-created/$container/$name"
  setting "az/$action-exit" 0
  if (( REPLY != 0 )); then
    printf 'ERROR: the stand-in Azure CLI fails %s as the scenario says\n' "$action" >&2
    return "$REPLY"
  fi
  case "$action" in
    upload)
      [[ "$file" == "$target" && -f "$source" ]] || unexpected azure-cli "$@"
      if [[ -e "$blob" ]]; then
        printf 'ERROR: The specified blob already exists.\nErrorCode:BlobAlreadyExists\n' >&2
        return 1
      fi
      mkdir -p -- "${blob%/*}" "${created%/*}"
      cp -- "$source" "$blob"
      setting epoch ''
      real date -u -d "@$REPLY" '+%Y-%m-%dT%H:%M:%S+00:00' > "$created"
      ;;
    show)
      if [[ ! -f "$blob" ]]; then
        printf 'ERROR: The specified blob does not exist.\n' >&2
        return 3
      fi
      setting az/show ''
      if [[ -n "$REPLY" ]]; then
        printf '%s\n' "$REPLY"
      else
        local length md5
        setting az/show-length "$(wc -c < "$blob" | tr -d '[:space:]')"
        length="$REPLY"
        setting az/show-md5 "$(openssl dgst -md5 -binary "$blob" | base64)"
        md5="$REPLY"
        printf '%s\t%s\n' "$length" "$md5"
      fi
      ;;
    list)
      if [[ -d "$STATE/blobs/$container" ]]; then
        while IFS= read -r name; do
          name="${name#./}"
          printf '%s\t%s\n' "$name" "$(< "$STATE/blobs-created/$container/$name")"
        done < <(cd -- "$STATE/blobs/$container" && find . -type f | sort)
      fi
      ;;
    delete)
      setting az/delete-refuses ''
      if [[ -n "$REPLY" && "$name" == "$REPLY" ]]; then
        printf 'ERROR: the stand-in Azure CLI refuses to delete %s as the scenario says\n' "$name" >&2
        return 1
      fi
      rm -f -- "$blob" "$created"
      ;;
    download) cat -- "$blob" ;;
    *) unexpected azure-cli "$@" ;;
  esac
}

backup_compose() {
  local shown="$1"
  shift
  if [[ "${1:-}" == pull ]]; then
    record "$shown $*"
    setting docker/backup/pull-exit 0
    return "$REPLY"
  fi
  [[ "${1:-}" == run ]] || unexpected compose "$@"
  shift
  local -a recorded=(run)
  local source='' target='' client='' tenant='' service
  while (( $# > 0 )); do
    case "$1" in
      --rm | -T | --quiet-pull)
        recorded+=("$1")
        shift
        ;;
      -e)
        recorded+=(-e "$2")
        case "$2" in
          BACKUP_CLIENT_ID=*) client="${2#*=}" ;;
          BACKUP_TENANT_ID=*) tenant="${2#*=}" ;;
        esac
        shift 2
        ;;
      -v)
        source="${2%%:*}"
        target="${2#*:}"
        target="${target%:ro}"
        recorded+=(-v "${source##*/}:${2#*:}")
        shift 2
        ;;
      *) break ;;
    esac
  done
  service="${1:-}"
  shift
  record "$shown ${recorded[*]} $service $*"
  if [[ -e /dev/fd/9 ]]; then
    printf 'held %s %s\n' "$service" "$*" >> "$STATE/lock-states"
  else
    printf 'free %s %s\n' "$service" "$*" >> "$STATE/lock-states"
  fi
  case "$service" in
    azure-cli) blob_command "$source" "$target" "$client" "$tenant" "$@" ;;
    *) volume_command "$service" "$@" ;;
  esac
}

docker_volume() {
  record "docker volume $*"
  [[ "${1:-}" == inspect ]] || unexpected volume "$@"
  if [[ ! -d "$STATE/volumes/${*: -1}" ]]; then
    printf 'Error response from daemon: get %s: no such volume\n' "${*: -1}" >&2
    return 1
  fi
  printf '%s\n' "${*: -1}"
}

docker_compose() {
  local -a shown=(docker compose) env_files=()
  local backup=0
  while (( $# > 0 )); do
    case "$1" in
      --progress)
        shown+=(--progress "$2")
        shift 2
        ;;
      -f)
        shown+=(-f "$2")
        if [[ "$2" == compose.backup.yaml ]]; then
          backup=1
        fi
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
  if (( backup )); then
    backup_compose "${shown[*]}" "$@"
    return
  fi
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

docker_container() {
  record "docker $*"
  case "${1:-}" in
    ps)
      if [[ "$*" == *health=unhealthy* ]]; then
        if [[ -f "$STATE/docker/unhealthy" ]]; then
          cat -- "$STATE/docker/unhealthy"
        fi
      elif [[ -f "$STATE/docker/running" ]]; then
        cat -- "$STATE/docker/running"
      fi
      ;;
    inspect)
      setting "docker/started/${*: -1}" ''
      [[ -n "$REPLY" ]] || unexpected "$@"
      printf '%s\n' "$REPLY"
      ;;
    restart)
      setting "docker/restart-exit" 0
      if (( REPLY == 0 )); then
        printf '%s\n' "${*: -1}"
      fi
      return "$REPLY"
      ;;
  esac
}

stand_in_docker() {
  case "${1:-}" in
    compose) docker_compose "${@:2}" ;;
    image) docker_image "${@:2}" ;;
    volume) docker_volume "${@:2}" ;;
    ps | inspect | restart) docker_container "$@" ;;
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
    record "date $*"
    case "$*" in
      +%H%M)
        setting clock 1200
        printf '%s\n' "$REPLY"
        ;;
      '-u +%s')
        setting epoch ''
        [[ -n "$REPLY" ]] || unexpected "$@"
        printf '%s\n' "$REPLY"
        ;;
      '-u -d '*' +%s') PATH="${PATH#"${0%/*}:"}" date "$@" ;;
      '-u -d @'*) PATH="${PATH#"${0%/*}:"}" date "$@" ;;
      *) unexpected "$@" ;;
    esac
    ;;
  sleep) record "sleep $*" ;;
  *) unexpected "$@" ;;
esac
