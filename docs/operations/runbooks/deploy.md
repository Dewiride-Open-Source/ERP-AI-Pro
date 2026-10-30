# Runbook: releasing and deploying

A release is a tag `vX.Y.Z` on main's newest commit. The release workflow (`.github/workflows/release.yml`) builds the api, web and proxy images of that commit once, checks what they contain, pushes them to `ghcr.io/dewiride-open-source/erp-ai-pro/{api,web,proxy}` with their provenance and SBOM attestations, and publishes the GitHub release whose files the server reads. On the server, `scripts/deploy/deploy.sh` verifies a release and deploys it, and `scripts/deploy/rollback.sh` returns to the release that ran before. Design: [ADR-0053](../../adr/0053-the-release-public-images-a-dependabot-gate-attestations-verified-on-the-server-and-the-deployment-scripts.md); the server and its two accounts: [host setup runbook](host-setup.md); the migrator's commands and exit codes: [migrations runbook](migrations.md).

## Cutting a release

1. **Main is ready.** Everything the release carries is merged, and every check run on main's newest commit has finished. The workflow's `gate` job refuses a commit while any check run of it is unfinished, or while the newest run of any of its checks failed, besides the named checks of `ci-backend`, `ci-frontend`, `docker-build`, `e2e`, `codeql` and `roadmap`. That includes GitHub's own `submit-nuget`, the Dependabot runs that Dependabot attaches to main's newest commit on Monday mornings, and a scheduled `npm advisories` run. Once they have passed, re-run the refused run's failed jobs (`gh run rerun <run id> --failed`), which runs the gate again.
2. **Dependabot is cleared.** No Dependabot alert and no Dependabot pull request may be open (owner decision of 2026-10-19). Every update Dependabot proposes, base image digests and the BuildKit of `.github/buildkit/Dockerfile` included, is adopted or closed first. An adopted update that moves a package or a base image carries the regenerated notices (`node scripts/notices/generate.ts`), or `docker-build` fails on their drift, and its compose smoke test fails when a built image holds a package the notices leave out. The gate prints only counts and numbers, because a public repository's logs are public. Dependabot's alerts and pull requests describe main's newest commit alone, so the gate refuses any other commit: when it refuses a tag over Dependabot, a re-run of that run releases the same commit and passes only while main has not moved, and after adopting an update the release is a new tag on main's newest commit, which carries it.
3. **Push the tag** on main's newest commit, from a clone of the repository:

   ```bash
   git fetch origin
   git tag --annotate v0.1.0 --message "ERP-AI-Pro v0.1.0" origin/main
   git push origin v0.1.0
   ```

   Pushing a tag is one of the GitHub actions that needs the owner's permission (root `CLAUDE.md` §16).

The workflow then runs `gate`, `build` (one job per image), `attest` and `release`. It publishes the release `ERP-AI-Pro v0.1.0` with these files:

| File | Content |
|---|---|
| `release-images.env` | four lines: `IMAGE_TAG` (the version without the `v`) and `API_IMAGE_DIGEST`, `WEB_IMAGE_DIGEST`, `PROXY_IMAGE_DIGEST` |
| `<image>.provenance.sigstore.json` | the Sigstore bundle of the image's SLSA provenance |
| `<image>.sbom.sigstore.json` | the Sigstore bundle of the image's SBOM attestation |
| `<image>.spdx.json` | the SPDX 2.3 SBOM itself |

A run that fails part-way can be re-run. It deletes the drafts of its tag that `github-actions[bot]` left, and refuses a tag that a published release, or a draft by anyone else, already uses. Image versions that a failed run pushed stay in the registry without attestations: `deploy.sh` refuses them, and deleting them is the owner's decision.

### The first release

The first release creates the three packages, and GitHub creates a package private. Each `attest` job checks its image as `deploy.sh` does, reading the registry without credentials, so the first run stops at **Verify both attestations as deploy.sh does** before anything is published. Set each of `api`, `web` and `proxy` to public on the organisation's **Packages** tab: the package, **Package settings**, **Danger Zone**, **Change visibility**, **Public**. GitHub's REST API has no endpoint for a package's visibility, and a public package can never be made private again ([repository settings](../github-repository-settings.md#releases)). Then re-run the run's failed jobs (`gh run rerun <run id> --failed`), which checks the images again and publishes the release. A release that adds an image goes the same way.

## Before a deployment

- Review the SQL of the release's migrations and confirm that the database's point-in-time restore window covers the deployment ([migrations runbook](migrations.md), "Before a deployment").
- Install both client certificates first, then deploy straight away. Until the first deployment writes the release into `.env`, a start of `erp-ai-pro.service` and the nightly `erp-ai-pro-retain.service` fail on the missing digests ([host setup runbook](host-setup.md), "The client certificates").
- Deploy outside the half hour before the server's restart at 03:00 India time on a night an installed update waits for it, which `deploy.sh` and `rollback.sh` refuse.

## Deploying

Sign in as the deploy account (`ssh -i ~/.ssh/erp-ai-pro-deploy erp-ai-pro@<address>`, or `sudo -iu erp-ai-pro`). Check the release first, then deploy it:

```bash
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/deploy.sh v0.1.0 --dry-run
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/deploy.sh v0.1.0
```

The dry run checks the release and pulls its images, then stops, changing neither `.env` nor the checkout. A deployment does this, in order, and stops at the first step that fails:

1. It takes the lock `/srv/erp-ai-pro/deploy.lock`, waiting up to 14 minutes while another holder ends. `rollback.sh` takes the same lock, and so do `erp-ai-pro.service` and `erp-ai-pro-retain.service`, which wait for it; the stack's start at boot holds it for each attempt, at most 13 minutes, and tries again a minute after a failed one, so a script that waits takes the lock between two attempts.
2. It reads `.env` and `.env.previous`, fetches main and the tags from origin, and requires the tag's commit on `main` and no local change to a tracked file of the checkout.
3. It downloads `release-images.env` and the six bundles, without credentials, and reads `release-images.env` as data: exactly the four lines, the version equal to the tag's.
4. It verifies each image's provenance and SBOM bundle with the GitHub CLI against the downloaded bundles; the CLI still reads each image's manifest from ghcr.io, without credentials, which the public packages allow. Each must be signed by `release.yml` at that tag, on a GitHub-hosted runner, for that tag's commit, in the repository `Dewiride-Open-Source/ERP-AI-Pro`, whose id `1377214678` it compares as well.
5. It moves the checkout to the tag's commit and continues with that commit's own `deploy.sh`, which verifies the release again. A release that changes how it is deployed is thus deployed its own way.
6. It pulls the three images by digest.
7. It asks the release's migrator for the database's status (`migrator status`). Exit 4, a database that holds migrations this release does not know, stops the deployment.
8. It runs the release's migrator (`migrate`). When the migrator fails, the running containers are left untouched and the checkout goes back.
9. It writes the release's four values into `.env` and keeps the replaced release's in `.env.previous`. When either file cannot be written, nothing is rolled out and the checkout goes back; when the release's migrations were applied already, it asks the running release's migrator, as after a failed migration, and the fix is to free what stopped the write, such as disk space, and deploy the release again at once.
10. It rolls `api`, `web` and `proxy` with `docker compose up --detach --wait --no-deps --no-build --pull never`, so the migrator never runs a second time.
11. It reads the API's readiness through the proxy container (`http://api:8080/healthz/ready`), up to 24 times 5 seconds apart. `Healthy` succeeds. `Degraded` succeeds with a warning: the API serves, but App Configuration or the attachments storage does not answer.
12. It removes the images of the three repositories that belong to neither the new release nor the one in `.env.previous`.

It ends with `deployed v0.1.0 (commit <commit>): the API answers Healthy`, and exits 1 after any refusal or failure, naming what happened. A release that changes `infra/host`, `scripts/deploy/bootstrap.sh` or `scripts/deploy/lib/common.sh` makes it warn: once the deployment has run, the administrator runs the bootstrap again from a clone of the tag ([host setup runbook](host-setup.md), "Upkeep", "Re-running the bootstrap").

### The first deployment

The checkout the bootstrap left was made before `deploy.sh` existed. So the first deployment moves it to the release by hand, after the bootstrap of that release ran (it installs the GitHub CLI), the three packages are public ("The first release") and `.env` holds the go-live values:

```bash
git -C /srv/erp-ai-pro/ERP-AI-Pro fetch --tags origin
git -C /srv/erp-ai-pro/ERP-AI-Pro checkout --detach v0.1.0
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/deploy.sh v0.1.0 --dry-run
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/deploy.sh v0.1.0
```

Every later deployment starts from the checkout the previous one left.

## Rolling back

```bash
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/rollback.sh --dry-run
bash /srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/rollback.sh
```

`rollback.sh` returns to the release `.env.previous` names. It goes back only when:

- that release is not the one running;
- its tag's commit is on `main`, and the checkout has no local change;
- its three images are still on the server (a deployment keeps them);
- its migrator answers `status` with 0, every migration it knows applied and no other.

An older API against a newer database is not supported, so a rollback never crosses a migration. To return to a release older than the database, deploy a release that knows its migrations, or restore the database ([migrations runbook](migrations.md), "Recovering from a bad migration"). Then `rollback.sh` moves the checkout, swaps `.env` and `.env.previous`, rolls the three containers and reads the readiness as `deploy.sh` does. Running it again goes forward to the release it left.

## When a rollout fails

The containers of the new release did not all become healthy within 5 minutes, or the API did not answer `Healthy` or `Degraded` in time. What `deploy.sh` does next depends on whether the release applied migrations:

| Situation | What `deploy.sh` did | What to do |
|---|---|---|
| The release applied no migration, and the release in `.env.previous` answers `status` with 0 | It went back to that release: the checkout, `.env`, `.env.previous` as it was before the run, and the containers. It exits 1 and names the state the API answers in. | Read the new release's logs (below), fix the cause in a new release or in the setting it names, and deploy again. |
| The release applied migrations | It stayed on the new release, because no earlier release supports its schema. | Fix forward with a new release, or restore the database to the point before the deployment and then deploy the previous release ([migrations runbook](migrations.md), "Recovering from a bad migration"). |
| The release in `.env.previous` answers `status` with anything but 0 | It stayed on the new release, naming the exit code. | Exit 4: the database is ahead of that release, so fix forward. Exit 2 or 1: the previous release's migrator cannot read its configuration or the database; fix that first, then run `rollback.sh` or deploy again. |
| Going back failed too | Neither release became ready. | Read both releases' logs, fix the shared cause (a setting, a Key Vault reference, a client certificate, or the database's firewall rule `home`), then deploy the release that should run. |
| The release `.env` named already (deploying it again) | It stayed on that release, with the checkout, `.env` and `.env.previous` as they were, and left its containers for diagnosis. | Read the logs, fix the cause and deploy again, or run `rollback.sh` to go to the release `.env.previous` names. |
| The first deployment, with no earlier release | It left the new release in `.env` with its containers, for diagnosis. | Read the logs, fix the cause, and deploy again. |

The containers' logs, from `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose`:

```bash
docker compose -f compose.yaml -f compose.production.yaml logs --since 30m api web proxy
docker compose -f compose.yaml -f compose.production.yaml ps
```

A failed migration, which `deploy.sh` reports before any container changes, is in the [migrations runbook](migrations.md), "When the migrator fails". When `migrate` fails part-way, the migrations it applied before the failure stay. `deploy.sh` then asks the running release's migrator, and an exit 4 there means the running API now faces a schema it does not know: fix forward, or restore the database.

## What the scripts refuse

| Message (shortened) | Cause | Action |
|---|---|---|
| run this as the deploy account erp-ai-pro | another account ran it | sign in as `erp-ai-pro` |
| run the copy of this script in the deployment checkout | a clone elsewhere ran it | run `/srv/erp-ai-pro/ERP-AI-Pro/scripts/deploy/deploy.sh` |
| another deployment or rollback, the nightly retention run or the stack's start at boot still holds the lock after 14 minutes | one of them runs on | wait for it (`systemctl status erp-ai-pro.service erp-ai-pro-retain.service`), then run it again |
| an installed update waits for the server's restart | less than half an hour to the 03:00 restart | run it after the restart; `/var/run/reboot-required.pkgs` names the updates |
| is not on main | the tag names a commit outside `main` | release from `main` only |
| the deployment checkout has local changes | a tracked file was edited on the server | put it back with `git -C /srv/erp-ai-pro/ERP-AI-Pro checkout -- <file>` |
| gh … is older than 2.102.0, or gh does not run | the GitHub CLI is missing or old | run the bootstrap again ([host setup runbook](host-setup.md)) |
| cannot download … of v0.1.0 | the release is not published, or one of its files is missing | wait for the release workflow, or re-run it |
| the images of v0.1.0 are not the ones its release workflow built | a bundle does not verify | deploy nothing; find out why before anything else, because a verified release never fails here |
| fix … .env first, or fix or remove … .env.previous | a release value in the file is not in its exact form | put the four lines back as `deploy.sh` wrote them, or remove `.env.previous` when no release ran before |
| the migrator of v0.1.0 finds migrations it does not know applied (exit 4) | the database belongs to a newer release | deploy that release or a later one |

## Files the scripts keep

| File | Content |
|---|---|
| `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose/.env` | the stack's values; `deploy.sh` and `rollback.sh` write its four release lines, `IMAGE_TAG`, `API_IMAGE_DIGEST`, `WEB_IMAGE_DIGEST` and `PROXY_IMAGE_DIGEST`, and nobody edits them by hand. `compose.production.yaml` refuses to start without them |
| `/srv/erp-ai-pro/ERP-AI-Pro/infra/compose/.env.previous` | the four values of the release before, 0600, read by `rollback.sh` and by a failed rollout going back |
| `/srv/erp-ai-pro/deploy.lock` | the lock the two scripts and the two units share; its content is empty |

Both files are written through a staged copy and one rename, so an interruption leaves each either as it was or complete.
