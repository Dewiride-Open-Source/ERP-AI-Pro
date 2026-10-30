#!/usr/bin/env bash

if [[ -n "${ERP_DEPLOY_RELEASE_SH_LOADED:-}" ]]; then
  return 0
fi
readonly ERP_DEPLOY_RELEASE_SH_LOADED=1

# shellcheck source=lib/common.sh
source "$(dirname -- "${BASH_SOURCE[0]}")/common.sh"

readonly RELEASE_REPOSITORY='Dewiride-Open-Source/ERP-AI-Pro'
# With --bundle, gh compares the repository by name alone, and a name can pass to another repository; its id cannot.
readonly RELEASE_REPOSITORY_ID='1377214678'
readonly RELEASE_REGISTRY='ghcr.io/dewiride-open-source/erp-ai-pro'
readonly RELEASE_IMAGES=(api web proxy)
readonly RELEASE_KEYS=(IMAGE_TAG API_IMAGE_DIGEST WEB_IMAGE_DIGEST PROXY_IMAGE_DIGEST)
readonly RELEASE_KEY_LINE='^[[:space:]]*(export[[:space:]]+)?(IMAGE_TAG|API_IMAGE_DIGEST|WEB_IMAGE_DIGEST|PROXY_IMAGE_DIGEST)[[:space:]]*[=:]'
readonly RELEASE_IMAGES_ASSET='release-images.env'
readonly RELEASE_ASSETS=(
  "$RELEASE_IMAGES_ASSET"
  api.provenance.sigstore.json api.sbom.sigstore.json
  web.provenance.sigstore.json web.sbom.sigstore.json
  proxy.provenance.sigstore.json proxy.sbom.sigstore.json
)
readonly PROVENANCE_PREDICATE='https://slsa.dev/provenance/v1'
readonly SBOM_PREDICATE='https://spdx.dev/Document/v2.3'
readonly TAG_PATTERN='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
readonly VERSION_PATTERN='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
readonly DIGEST_PATTERN='^sha256:[0-9a-f]{64}$'
readonly COMMIT_PATTERN='^[0-9a-f]{40}$'
readonly MAX_RELEASE_FILE_BYTES=1024
readonly MAX_ASSET_BYTES=67108864

# tests/release-decisions.sh runs these scripts in a folder that stands for this server's root.
readonly HOST_ROOT="${ERP_DEPLOY_TEST_ROOT:-}"
readonly RELEASE_CHECKOUT="$HOST_ROOT$DEPLOY_CHECKOUT"
readonly RELEASE_LOCK="$HOST_ROOT$DEPLOY_LOCK"
readonly REBOOT_REQUIRED_FILE="$HOST_ROOT/var/run/reboot-required"
readonly COMPOSE_DIR="$REPO_ROOT/infra/compose"
readonly ENV_FILE="$COMPOSE_DIR/.env"
readonly PREVIOUS_ENV_FILE="$COMPOSE_DIR/.env.previous"
readonly NIGHTLY_UPDATES_SETTINGS="$HOST_CONFIG_DIR/apt/52erp-ai-pro-unattended-upgrades.conf"
readonly ROLLOUT_WAIT_SECONDS=300
readonly RELEASE_LOCK_WAIT_SECONDS=840
readonly READINESS_ATTEMPTS=24
readonly READINESS_INTERVAL_SECONDS=5
readonly RESTART_WINDOW_MINUTES=30
readonly INDIA_OFFSET_MINUTES=330

GH_VERSION_CHECKED=0
READINESS_STATE=''

is_one_of() {
  local wanted="$1" item
  shift
  for item in "$@"; do
    if [[ "$item" == "$wanted" ]]; then
      return 0
    fi
  done
  return 1
}

printable() {
  local text="${1//[^[:alnum:] ._:\/@=+-]/?}"
  printf '%s' "${text:0:120}"
}

release_version_of_tag() {
  local tag="$1"
  if (( ${#tag} > 32 )) || [[ ! "$tag" =~ $TAG_PATTERN ]]; then
    return 1
  fi
  printf '%s' "${tag#v}"
}

release_value_valid() {
  local key="$1" value="$2"
  if [[ "$key" == IMAGE_TAG ]]; then
    (( ${#value} <= 31 )) && [[ "$value" =~ $VERSION_PATTERN ]]
  else
    [[ "$value" =~ $DIGEST_PATTERN ]]
  fi
}

release_value_shape() {
  if [[ "$1" == IMAGE_TAG ]]; then
    printf 'a version such as 0.1.0'
  else
    printf 'sha256: followed by 64 lower-case hexadecimal digits'
  fi
}

release_lines() {
  local version api web proxy
  read -r version api web proxy <<< "$1"
  printf 'IMAGE_TAG=%s\nAPI_IMAGE_DIGEST=%s\nWEB_IMAGE_DIGEST=%s\nPROXY_IMAGE_DIGEST=%s\n' "$version" "$api" "$web" "$proxy"
}

release_digests() {
  local api web proxy
  read -r _ api web proxy <<< "$1"
  printf '%s\n' "$api" "$web" "$proxy"
}

# A release travels as one line, "<version> <api digest> <web digest> <proxy digest>", made only of values that matched their
# patterns, so it reaches no URL, path or command line unchecked. Files are read as data and never sourced.
release_of_file() {
  local file="$1" line key value number=0 size
  local -A values=()
  if [[ ! -f "$file" ]]; then
    log_error "$file is missing"
    return 1
  fi
  size="$(wc -c < "$file")"
  if (( size > MAX_RELEASE_FILE_BYTES )); then
    log_error "$file holds $size bytes, more than the four release values ever take"
    return 1
  fi
  while IFS= read -r line || [[ -n "$line" ]]; do
    number=$(( number + 1 ))
    if (( number > ${#RELEASE_KEYS[@]} )); then
      log_error "$file holds more lines than the four ${RELEASE_KEYS[*]}"
      return 1
    fi
    if [[ ! "$line" =~ ^(IMAGE_TAG|API_IMAGE_DIGEST|WEB_IMAGE_DIGEST|PROXY_IMAGE_DIGEST)=(.*)$ ]]; then
      log_error "line $number of $file is not KEY=value for one of ${RELEASE_KEYS[*]}"
      return 1
    fi
    key="${BASH_REMATCH[1]}"
    value="${BASH_REMATCH[2]}"
    if [[ -n "${values[$key]+set}" ]]; then
      log_error "$file sets $key twice"
      return 1
    fi
    if ! release_value_valid "$key" "$value"; then
      log_error "$file sets $key to a value that is not $(release_value_shape "$key")"
      return 1
    fi
    values[$key]="$value"
  done < "$file"
  if (( number != ${#RELEASE_KEYS[@]} )); then
    log_error "$file holds $number line(s), not the four ${RELEASE_KEYS[*]}"
    return 1
  fi
  printf '%s %s %s %s\n' "${values[IMAGE_TAG]}" "${values[API_IMAGE_DIGEST]}" "${values[WEB_IMAGE_DIGEST]}" "${values[PROXY_IMAGE_DIGEST]}"
}

saved_release() {
  if [[ -e "$1" ]]; then
    release_of_file "$1"
  fi
}

# .env holds the stack's other values too; of the four release values, IMAGE_TAG=local or empty with no digests names no release.
release_of_env_file() {
  local file="$1" line key value number=0 tag digests
  local -A values=()
  if [[ ! -f "$file" ]]; then
    log_error "$file is missing; bootstrap.sh writes it from .env.example"
    return 1
  fi
  while IFS= read -r line || [[ -n "$line" ]]; do
    number=$(( number + 1 ))
    if [[ ! "$line" =~ $RELEASE_KEY_LINE ]]; then
      continue
    fi
    key="${BASH_REMATCH[2]}"
    if [[ ! "$line" =~ ^$key=(.*)$ ]]; then
      log_error "line $number of $file sets $key in another form than $key=<value>, the one deploy.sh writes"
      return 1
    fi
    value="${BASH_REMATCH[1]}"
    if [[ -n "${values[$key]+set}" ]]; then
      log_error "$file sets $key twice"
      return 1
    fi
    values[$key]="$value"
  done < "$file"
  tag="${values[IMAGE_TAG]:-}"
  digests="${values[API_IMAGE_DIGEST]:-}${values[WEB_IMAGE_DIGEST]:-}${values[PROXY_IMAGE_DIGEST]:-}"
  if [[ ( -z "$tag" || "$tag" == local ) && -z "$digests" ]]; then
    return 0
  fi
  for key in "${RELEASE_KEYS[@]}"; do
    if ! release_value_valid "$key" "${values[$key]:-}"; then
      log_error "$file names no whole release: $key is not $(release_value_shape "$key"), and deploy.sh writes IMAGE_TAG and the three image digests together"
      return 1
    fi
  done
  printf '%s %s %s %s\n' "${values[IMAGE_TAG]}" "${values[API_IMAGE_DIGEST]}" "${values[WEB_IMAGE_DIGEST]}" "${values[PROXY_IMAGE_DIGEST]}"
}

# The staged file takes the target's place in one rename, so a crash leaves one whole file or the other, never part of one.
write_lines_atomically() {
  local target="$1" staged
  shift
  if ! staged="$(mktemp "$target.staged.XXXXXX")"; then
    log_error "cannot create a file next to $target"
    return 1
  fi
  if printf '%s\n' "$@" > "$staged" && chmod 600 -- "$staged" && mv -f -- "$staged" "$target"; then
    return 0
  fi
  rm -f -- "$staged"
  log_error "cannot write $target"
  return 1
}

write_release_file() {
  local -a lines=()
  mapfile -t lines < <(release_lines "$2")
  write_lines_atomically "$1" "${lines[@]}"
}

write_env_release() {
  local file="$1" line key
  local -a lines=() release=()
  local -A wanted=() written=()
  mapfile -t release < <(release_lines "$2")
  for line in "${release[@]}"; do
    wanted[${line%%=*}]="$line"
  done
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ "$line" =~ $RELEASE_KEY_LINE ]]; then
      key="${BASH_REMATCH[2]}"
      if [[ -z "${written[$key]+set}" ]]; then
        lines+=("${wanted[$key]}")
        written[$key]=1
      fi
    else
      lines+=("$line")
    fi
  done < "$file"
  for key in "${RELEASE_KEYS[@]}"; do
    if [[ -z "${written[$key]+set}" ]]; then
      lines+=("${wanted[$key]}")
    fi
  done
  write_lines_atomically "$file" "${lines[@]}"
}

# Docker runs with an environment as bare as the systemd units' own: a variable of the caller's would override the env files,
# a COMPOSE_ variable would change the project or its files, and DOCKER_HOST would name another daemon.
host_docker() {
  env -i HOME="$HOME" PATH="$PATH" docker "$@"
}

# gh runs as bare: an XDG_CONFIG_HOME or GH_CONFIG_DIR of the caller's would point it at a configuration it may not read,
# which stops it before it verifies anything, and GH_HOST or GH_TOKEN would change where and as whom it asks.
host_gh() {
  env -i HOME="$HOME" PATH="$PATH" GH_NO_UPDATE_NOTIFIER=1 gh "$@"
}

release_compose() {
  local release_file="$1"
  shift
  local -a env_files=(--env-file .env)
  if [[ -n "$release_file" ]]; then
    env_files+=(--env-file "$release_file")
  fi
  (cd -- "$COMPOSE_DIR" && host_docker compose -f compose.yaml -f compose.production.yaml "${env_files[@]}" "$@")
}

migrator_status_meaning() {
  case "$1" in
    0) printf 'finds every migration it knows applied and no other' ;;
    2) printf 'could not load its configuration (exit 2)' ;;
    3) printf 'finds migrations of its own still to apply (exit 3)' ;;
    4) printf 'finds migrations it does not know applied (exit 4): the database belongs to a newer release' ;;
    *) printf 'could not report on the database (exit %s)' "$1" ;;
  esac
}

await_readiness() {
  local attempt answer
  READINESS_STATE=''
  for (( attempt = 1; attempt <= READINESS_ATTEMPTS; attempt++ )); do
    if answer="$(release_compose '' exec -T proxy wget -qO- -T 5 http://api:8080/healthz/ready 2> /dev/null)" \
      && [[ "$answer" == Healthy || "$answer" == Degraded ]]; then
      READINESS_STATE="$answer"
      log_info "the API answers $answer to the proxy"
      return 0
    fi
    if (( attempt < READINESS_ATTEMPTS )); then
      sleep "$READINESS_INTERVAL_SECONDS"
    fi
  done
  log_error "the API did not answer Healthy or Degraded at http://api:8080/healthz/ready from the proxy in $READINESS_ATTEMPTS tries $READINESS_INTERVAL_SECONDS seconds apart"
  return 1
}

roll_out() {
  if ! release_compose '' up --detach --wait --wait-timeout "$ROLLOUT_WAIT_SECONDS" --no-deps --no-build --pull never api web proxy; then
    log_error "the api, web and proxy containers were not all running and healthy within $ROLLOUT_WAIT_SECONDS seconds"
    return 1
  fi
  await_readiness
}

# A flock(2) lock belongs to the open file, which exec keeps open, so the deploy script that deploy.sh continues with holds it.
# erp-ai-pro.service holds the lock for each attempt to start the stack, at most TimeoutStartSec (10 minutes) and TimeoutStopSec
# (3) together, and tries again RestartSec (1 minute) after a failed one, so a run that waits 14 minutes takes it between two.
acquire_release_lock() {
  if [[ "${ERP_DEPLOY_LOCK_PID:-}" == "$$" ]]; then
    flock --nonblock 9 || die "this run lost the lock on $RELEASE_LOCK that the deploy script it continues took"
    return 0
  fi
  if [[ ! -e "$RELEASE_LOCK" ]]; then
    : >> "$RELEASE_LOCK" || die "cannot create $RELEASE_LOCK"
  fi
  exec 9< "$RELEASE_LOCK" || die "cannot open $RELEASE_LOCK"
  if ! flock --nonblock 9; then
    log_info "another deployment or rollback, the nightly retention run or the stack's start at boot holds $RELEASE_LOCK; waiting up to $(( RELEASE_LOCK_WAIT_SECONDS / 60 )) minutes for it (systemctl status erp-ai-pro.service erp-ai-pro-retain.service)"
    flock --wait "$RELEASE_LOCK_WAIT_SECONDS" 9 \
      || die "another deployment or rollback, the nightly retention run or the stack's start at boot still holds $RELEASE_LOCK after $(( RELEASE_LOCK_WAIT_SECONDS / 60 )) minutes; run this again once it ends (systemctl status erp-ai-pro.service erp-ai-pro-retain.service)"
  fi
  export ERP_DEPLOY_LOCK_PID="$$"
}

# unattended-upgrades reads Automatic-Reboot-Time on the host's clock, which bootstrap.sh keeps in UTC.
refuse_before_the_nightly_restart() {
  if [[ ! -e "$REBOOT_REQUIRED_FILE" ]]; then
    return 0
  fi
  local setting restart now minutes india
  setting="$(sed -n 's/^Unattended-Upgrade::Automatic-Reboot-Time "\([0-9][0-9]:[0-9][0-9]\)";$/\1/p' "$NIGHTLY_UPDATES_SETTINGS" 2> /dev/null || true)"
  [[ "$setting" =~ ^([01][0-9]|2[0-3]):([0-5][0-9])$ ]] || die "cannot read the nightly restart time from $NIGHTLY_UPDATES_SETTINGS"
  restart=$(( 10#${BASH_REMATCH[1]} * 60 + 10#${BASH_REMATCH[2]} ))
  now="$(date +%H%M || true)"
  [[ "$now" =~ ^([01][0-9]|2[0-3])([0-5][0-9])$ ]] || die "cannot read this server's clock"
  minutes=$(( (restart - (10#${BASH_REMATCH[1]} * 60 + 10#${BASH_REMATCH[2]}) + 1440) % 1440 ))
  if (( minutes <= RESTART_WINDOW_MINUTES )); then
    india=$(( (restart + INDIA_OFFSET_MINUTES) % 1440 ))
    die "an installed update waits for the server's restart at $setting UTC ($(printf '%02d:%02d' $(( india / 60 )) $(( india % 60 ))) India time), $minutes minute(s) from now, which would cut this run short; run it again once the server is back (/var/run/reboot-required.pkgs names the updates)"
  fi
}

require_release_host() {
  local account
  account="$(id -un)"
  [[ "$account" == "$DEPLOY_USER" ]] \
    || die "run this as the deploy account $DEPLOY_USER, not as $account: sign in with its key, or sudo -iu $DEPLOY_USER (docs/operations/runbooks/deploy.md)"
  [[ "$REPO_ROOT" == "$RELEASE_CHECKOUT" ]] \
    || die "run the copy of this script in the deployment checkout $RELEASE_CHECKOUT, which deployments move; this one is in $REPO_ROOT"
}

repo_git() {
  git -C "$REPO_ROOT" "$@"
}

head_commit() {
  repo_git rev-parse --verify HEAD
}

release_commit() {
  local commit
  commit="$(repo_git rev-parse --verify --quiet "refs/tags/$1^{commit}")" || return 1
  [[ "$commit" =~ $COMMIT_PATTERN ]] || return 1
  printf '%s' "$commit"
}

require_on_main() {
  local commit="$1" name="$2" status=0
  repo_git merge-base --is-ancestor "$commit" refs/remotes/origin/main || status=$?
  case "$status" in
    0) ;;
    1) die "$name (commit ${commit:0:12}) is not on main, and only a release cut from main is deployed" ;;
    *) die "cannot tell whether $name is on main (git merge-base exit $status)" ;;
  esac
}

require_clean_checkout() {
  local changes
  changes="$(repo_git status --porcelain --untracked-files=no)" || die "cannot read the state of the deployment checkout"
  [[ -z "$changes" ]] \
    || die "the deployment checkout has local changes, which no release carries: $(paste -sd ' ' - <<< "$changes"); put them back with git -C $REPO_ROOT checkout -- <file> and run this again"
}

move_checkout() {
  repo_git -c advice.detachedHead=false checkout --quiet --detach "$1"
}

download_release_assets() {
  local tag="$1" folder="$2" asset
  for asset in "${RELEASE_ASSETS[@]}"; do
    if ! curl -fsSL --retry 3 --proto '=https' --proto-redir '=https' --max-time 300 --max-filesize "$MAX_ASSET_BYTES" \
      --output "$folder/$asset" "https://github.com/$RELEASE_REPOSITORY/releases/download/$tag/$asset"; then
      log_error "cannot download $asset of $tag (see curl's error above); a release is deployed only once all its assets are published"
      return 1
    fi
    log_info "$asset"
  done
}

version_at_least() {
  local index
  local -a have=() want=()
  IFS=. read -r -a have <<< "$1"
  IFS=. read -r -a want <<< "$2"
  for index in 0 1 2; do
    if (( 10#${have[$index]} != 10#${want[$index]} )); then
      (( 10#${have[$index]} > 10#${want[$index]} ))
      return
    fi
  done
  return 0
}

require_gh_version() {
  if (( GH_VERSION_CHECKED )); then
    return 0
  fi
  local output first version
  if ! output="$(host_gh --version 2> /dev/null)"; then
    log_error "gh, the GitHub CLI, does not run here; bootstrap.sh installs it"
    return 1
  fi
  first="${output%%$'\n'*}"
  if [[ ! "$first" =~ ^gh\ version\ ([0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6})([^0-9.]|$) ]]; then
    log_error "cannot read gh's version from '$(printable "$first")'"
    return 1
  fi
  version="${BASH_REMATCH[1]}"
  if ! version_at_least "$version" "$MINIMUM_GH_VERSION"; then
    log_error "gh $version is older than $MINIMUM_GH_VERSION, the first release whose checks of the signing workflow and its ref these scripts rely on; bootstrap.sh installs a newer one"
    return 1
  fi
  GH_VERSION_CHECKED=1
}

# The policy every image of a release meets, checked by deploy.sh and by the release workflow on its own bundles: provenance or
# an SBOM signed by release.yml for exactly this tag and commit, on a GitHub-hosted runner, in this repository.
verify_release_image() {
  local image="$1" digest="$2" bundle="$3" predicate="$4" tag="$5" commit="$6" kind output line
  if ! is_one_of "$image" "${RELEASE_IMAGES[@]}"; then
    log_error "'$(printable "$image")' is not an image of a release (${RELEASE_IMAGES[*]})"
    return 1
  fi
  case "$predicate" in
    "$PROVENANCE_PREDICATE") kind=provenance ;;
    "$SBOM_PREDICATE") kind=SBOM ;;
    *)
      log_error "'$(printable "$predicate")' is neither the provenance's predicate type nor the SBOM's"
      return 1
      ;;
  esac
  if [[ ! "$digest" =~ $DIGEST_PATTERN || ! "$commit" =~ $COMMIT_PATTERN ]] || ! release_version_of_tag "$tag" > /dev/null; then
    log_error "the $kind of the $image image needs a digest sha256:<64 hex digits>, a release tag such as v0.1.0 and a 40-digit commit"
    return 1
  fi
  if [[ ! -f "$bundle" || ! -r "$bundle" ]]; then
    log_error "cannot read the $kind bundle $bundle"
    return 1
  fi
  require_gh_version || return 1
  if ! output="$(host_gh attestation verify "oci://$RELEASE_REGISTRY/$image@$digest" \
    --repo "$RELEASE_REPOSITORY" \
    --bundle "$bundle" \
    --signer-workflow "github.com/$RELEASE_REPOSITORY/.github/workflows/release.yml@refs/tags/$tag" \
    --source-ref "refs/tags/$tag" \
    --source-digest "$commit" \
    --deny-self-hosted-runners \
    --predicate-type "$predicate" \
    --format json \
    --jq '.[].verificationResult.signature.certificate.sourceRepositoryIdentifier')"; then
    log_error "gh refused the $kind of the $image image $digest for $tag (see its error above)"
    return 1
  fi
  if [[ -z "$output" ]]; then
    log_error "gh verified no $kind of the $image image $digest"
    return 1
  fi
  while IFS= read -r line; do
    if [[ "$line" != "$RELEASE_REPOSITORY_ID" ]]; then
      log_error "the $kind of the $image image $digest was signed in the repository with the id '$(printable "$line")', not in $RELEASE_REPOSITORY ($RELEASE_REPOSITORY_ID)"
      return 1
    fi
  done <<< "$output"
  log_info "the $kind of the $image image $digest, signed by the release workflow for $tag at ${commit:0:12}"
}

verify_release_images() {
  local tag="$1" commit="$2" folder="$3" index image
  local -a digests=()
  mapfile -t digests < <(release_digests "$4")
  for index in "${!RELEASE_IMAGES[@]}"; do
    image="${RELEASE_IMAGES[$index]}"
    verify_release_image "$image" "${digests[$index]}" "$folder/$image.provenance.sigstore.json" "$PROVENANCE_PREDICATE" "$tag" "$commit" || return 1
    verify_release_image "$image" "${digests[$index]}" "$folder/$image.sbom.sigstore.json" "$SBOM_PREDICATE" "$tag" "$commit" || return 1
  done
}

missing_release_images() {
  local index image
  local -a digests=()
  mapfile -t digests < <(release_digests "$1")
  for index in "${!RELEASE_IMAGES[@]}"; do
    image="${RELEASE_IMAGES[$index]}"
    if ! host_docker image inspect --format '{{.Id}}' "$RELEASE_REGISTRY/$image@${digests[$index]}" > /dev/null 2>&1; then
      printf '%s\n' "$image"
    fi
  done
}

prune_release_images() {
  local image digest
  local -a keep=("$@")
  for image in "${RELEASE_IMAGES[@]}"; do
    while IFS= read -r digest; do
      if [[ ! "$digest" =~ $DIGEST_PATTERN ]] || is_one_of "$digest" "${keep[@]}"; then
        continue
      fi
      if host_docker image rm "$RELEASE_REGISTRY/$image@$digest" > /dev/null; then
        log_info "removed the $image image $digest"
      else
        log_warn "kept the $image image $digest, which Docker would not remove (see its error above)"
      fi
    done < <(host_docker image ls --digests --format '{{.Digest}}' "$RELEASE_REGISTRY/$image" | sort -u)
  done
}
