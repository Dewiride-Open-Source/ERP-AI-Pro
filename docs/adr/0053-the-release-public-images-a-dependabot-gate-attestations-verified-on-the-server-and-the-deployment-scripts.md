# ADR-0053: The release: public images, a Dependabot gate, attestations verified on the server, and the deployment scripts

Status: accepted
Date: 2026-10-19

## Context

- **What existed.** `docker-build.yml` built the api, web and proxy images and smoke-tested the compose stack, but published nothing. A deployment was the manual sequence of `docs/operations/runbooks/migrations.md`: move the server's checkout, set `IMAGE_TAG`, pull, run the migrator on its own, then `up` ([ADR-0021](0021-migrator-program-and-seeding.md)). `scripts/deploy/bootstrap.sh` prepares the server ([ADR-0049](0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md)): a deploy account `erp-ai-pro` without sudo, the only member of `docker`, and the units `erp-ai-pro.service` (brings the last deployment back at boot) and `erp-ai-pro-retain.timer` (the migrator's `retain` every night at 01:00 India time). [ADR-0030](0030-community-and-enterprise-editions.md) left the obligations of conveying the Community images to this item: COPYING and COPYING.LESSER in the images, third-party notices, OCI labels, the tagged commit named as the corresponding source, and no `Microsoft.Identity.Client.NativeInterop`.
- **Owner decisions of 2026-10-19.**
  - The images are public on ghcr.io. A public package can never be made private again. Each release lists every open-source part inside, and a lawyer should confirm that this meets the GPL duties of the base images' operating-system packages.
  - No image vulnerability scanner. Grype was recommended (Apache-2.0, Anchore, no account; no Microsoft or GitHub option scans ghcr.io images without a paid plan and a client secret), and Trivy was offered, whose distribution was compromised in March 2026 (GHSA-69fq-xp46-6x23, critical). The owner answered "no need to do this because dependabot needs to be cleared before we release".
- **Facts that shaped the design** (official sources, read on 2026-10-19).
  - Dependabot alerts are read with the `GITHUB_TOKEN` permission `vulnerability-alerts: read` (docs.github.com, workflow syntax; `security-events` does not cover them). Dependabot raises version updates, not security updates, for Docker, and the GitHub Advisory Database covers no operating-system package ecosystem, so no alert ever names a package of an image's base layers. Its docker updates move the pinned base image digests, which bring their fixes.
  - `actions/attest` v4.2.2 writes SLSA provenance, and with `sbom-path` an SBOM attestation, signed with Sigstore's public-good instance for a public repository. `gh attestation verify` skips its sign-in check only with `--bundle`; versions before 2.102.0 compared `--source-ref` without regard to case and `--signer-workflow` as a prefix (GHSA-4mq3-hpgx-9cx8, GHSA-wjmr-j3rp-mh2g). With `--bundle` the repository is compared only by name, so the certificate's `sourceRepositoryIdentifier` is checked against the repository's id, `1377214678`.
  - BuildKit's own `sbom: true` runs Docker's syft scanner image, a dependency the owner has not approved. Microsoft's `sbom-tool` (`-di`) reports only the operating-system packages and runs a Syft container with the Docker socket mounted, which would be root next to the signing token.
  - `Microsoft.Identity.Client.NativeInterop` (brought in by `Microsoft.Identity.Client.Broker` through `Microsoft.Data.SqlClient.Extensions.Azure`) is licensed under terms whose section 3(e) forbids distributing the software. On Linux nothing loads it: SqlClient calls `WithBroker` only for the Windows public-client flows, and this repository uses `Active Directory Default` alone.
  - sharp's libvips (`@img/sharp-libvips-linux-x64`, LGPL-3.0-or-later) statically links cairo, MPL-1.1, which the licence policy of ADR-0030 does not allow, and the web app uses no `next/image`.
  - The proxy's Alpine base carries busybox and apk-tools (GPL-2.0-only). The Debian `node:24-slim` and Ubuntu chiseled bases carry GPL and LGPL packages too. The caddy base image sets OCI labels of its own (`vendor=Light Code Labs`, `licenses=Apache-2.0`, `title=Caddy`), which an image inherits.
  - `docker compose up` recreates a changed api before it evaluates the api's dependency on the one-shot migrator, and `up` without `--no-deps` runs the migrator, with its seeders and sweeps, a second time (ADR-0021). An older API against a newer schema is not supported (`migrations.md`).

## Decision

- **The release workflow** (`.github/workflows/release.yml`), on a pushed tag `vX.Y.Z`, every action pinned to a full SHA, every checkout without persisted credentials:
  - `gate`:
    - the tag is a semantic version naming `GITHUB_SHA`, and the commit is on `main`;
    - the 23 check runs that run on a push to `main` are present and passed, no other check run of the commit is unfinished, and the newest run of every other check passed (the runs of one workflow ordered by run number; Dependabot's told apart by their whole title less ` - Update #n`, which names the ecosystem and directories and, for a run that refreshes one pull request, that pull request's dependencies); runs of `release.yml` itself are left out;
    - the commit is main's newest commit when Dependabot is read, because Dependabot's alerts and pull requests describe that commit alone, so neither an older commit nor a re-run after an update was merged ships what the update replaced;
    - no Dependabot alert and no Dependabot pull request is open, printing only counts and numbers because a public repository's logs are public.
    The list of required names changes whenever a job of `ci-backend`, `ci-frontend`, `docker-build`, `e2e`, `codeql` or `roadmap` is renamed or added.
  - `build` (per image; `packages: write`):
    - BuildKit pinned by version and digest (`moby/buildkit:v0.34.1@sha256:99368f3e…`) in `.github/buildkit/Dockerfile`, which nothing builds and Dependabot's docker ecosystem keeps current, the BuildKit `docker-build` runs too; `SOURCE_DATE_EPOCH` and the build arguments from the tag and its commit; no cache;
    - build once into the local daemon, run the image contents check and the SBOM generator on that image, then push it from the same builder;
    - fail unless both builds have the same configuration digest and the pushed manifest references it, so the attested digest is the image that was checked.
  - `attest` (`id-token: write`, `attestations: write`, running no third-party container):
    - `actions/attest` twice per digest: SLSA provenance, and the SPDX 2.3 SBOM; both pushed to the registry;
    - then, signed out of the registry, a self-check with the very function `deploy.sh` uses (`verify_release_image`, `scripts/deploy/lib/release.sh`), which reads the image as the server does, so a package that is not public stops the release before it is published.
  - `release` (`contents: write`, `pull-requests: read`, `vulnerability-alerts: read`):
    - main's newest commit and Dependabot are read again as the gate reads them, because a re-run of only the failed jobs keeps the gate's answers of the first attempt;
    - a draft release with `release-images.env` (the tag and the three digests), the six bundles and the three SBOMs, then published;
    - the notes name the tagged commit as the corresponding source.
- **The images.**
  - Labels: final-stage `org.opencontainers.image.*` labels from the build arguments `ERP_VERSION`, `ERP_REVISION` and `ERP_CREATED`. The vendor is Dewiride Technologies Private Limited, the source and url the repository, and the licence `LGPL-3.0-only`, or `LGPL-3.0-only AND Apache-2.0` for the proxy, which overrides every label its base sets.
  - `/usr/share/doc/erp-ai-pro/` holds COPYING, COPYING.LESSER and the image's third-party notices.
  - Guards in each Dockerfile's build stage keep the forbidden files out of every layer, and the image contents check (`scripts/checks/image-contents.ts`) reads every layer of `docker save` in the compose smoke test and in the release.
  - `Microsoft.Identity.Client.NativeInterop` is excluded from the published Api and Migrator hosts (`ExcludeAssets="all"`, central version 0.20.6, the one `Microsoft.Identity.Client.Broker` 4.91.1 requires).
  - The web app's standalone output leaves out sharp and `@img`, and ESLint refuses a value import of `next/image` (rule L7 of `docs/architecture/dependency-rules.md`).
- **The notices and the SBOM** come from one inventory (`scripts/notices`):
  - the application packages that reach each image, with their licence texts and copyright lines;
  - the runtimes, pointing at the notices files their base images carry;
  - the base images' operating-system packages, read from their package databases without running them, with their source locations;
  - each distinct licence text once.
  The notices are committed under `infra/docker/notices/` and checked for drift, and the compose smoke test of every `docker-build` run and of `verify.ts --docker` compares each built image with its notices as the SBOM generator does, so an image that holds a package its notices leave out fails on its pull request. The SBOM is generated from the built image itself at release time.
- **The deployment** (`scripts/deploy/deploy.sh <tag>`, as `erp-ai-pro`, holding one lock that `rollback.sh` and the systemd units share, which both scripts wait up to 14 minutes for, because the stack's start at boot holds it for each attempt, at most 13 minutes, and tries again a minute after a failed one):
  1. validate the tag; resolve its commit and require it on `origin/main`;
  2. download the release's assets without credentials and parse them strictly (never sourced);
  3. verify every image's provenance and SBOM bundles from the downloaded files (`--bundle`; the GitHub CLI still reads each image's manifest from the public registry, without credentials) against the release workflow at that tag, the tag, its commit and the repository's id;
  4. check out that commit (by SHA) and continue with the commit's own `deploy.sh`;
  5. pull the images by digest;
  6. read the target release's `migrator status`, refusing a database ahead of it;
  7. run the migrator on its own; a failure leaves the running stack untouched;
  8. keep the previous release's values in `infra/compose/.env.previous`, then roll `api`, `web` and `proxy` with `up -d --wait --no-deps`, so the migrator never runs twice;
  9. read the API's readiness through the proxy container: `Healthy` passes, `Degraded` passes with a warning;
  10. after a failed rollout that applied no migration, put the previous release back, but only when that release's own `migrator status` answers 0, at the commit of that release's tag; after one that did, stop and point at the runbook (a forward fix or a point-in-time restore); a rollout of the release `.env` already named stays on it, with `.env`, `.env.previous` and the checkout as they were.

  `rollback.sh` returns to `.env.previous` only while that release's `migrator status` answers 0 and its images are still on the server. `deploy.sh --dry-run` verifies the release and pulls its three images by digest, then stops; `rollback.sh --dry-run` checks that the rollback can run, its migrator's `status` included. Neither changes `.env`, `.env.previous`, the checkout's commit or the running containers. Both refuse the half hour before a restart that an installed update waits for, because a restart would cut a rollback short as much as a deployment.
- **Releases hand over to each other.** The running release's `deploy.sh` verifies the target, moves the checkout and `exec`s the target commit's `deploy.sh`, which verifies again with its own code. The handover is a contract that every later `deploy.sh` keeps, because the release it replaces starts it:
  - file descriptor 9 holds the lock;
  - `ERP_DEPLOY_LOCK_PID` is the process id, which `exec` keeps;
  - `ERP_DEPLOY_ASSETS` names the folder of the downloaded files;
  - `ERP_DEPLOY_RETURN_COMMIT` is the commit to go back to while `.env` still names the release it runs;
  - the continued script sets its EXIT trap and reads `ERP_DEPLOY_RETURN_COMMIT` before any check of its own, so a check that a later release adds still puts the checkout back when it refuses.
- **Tests of the scripts.** `scripts/deploy/tests/release-decisions.sh` runs both scripts, and the `verify_release_image` function the release workflow calls, against stand-ins for `docker`, `gh`, `curl`, `git`, `flock`, `wget`, `id`, `date`, `sleep` and `mv`, in a folder that `ERP_DEPLOY_TEST_ROOT` makes the server's root. On the server the variable is unset, and the scripts' own folder check refuses any other root.
- **The migrator's exit code 4.** `status`, `migrate` and `retain` first compare each catalogue context's applied migrations with the release's. When the database holds a migration the release does not know, they name it and exit 4 without doing anything else, so neither an older release's seeders and sweeps nor its API run against a newer schema.
- **The host.** The bootstrap installs the GitHub CLI (2.102.0 or later) from GitHub's apt repository, with its key checked by fingerprint, and adds its origin to unattended upgrades. The production compose file pulls every image by `IMAGE_TAG@<digest>`, from four `.env` values that `deploy.sh` writes.
- **Earlier decisions this changes.**
  - ADR-0021: the migrator's exit codes gain 4, and the manual sequence of `migrations.md` becomes `deploy.sh`.
  - ADR-0049: the bootstrap installs the GitHub CLI, and the units share the deployment lock.
  - ADR-0030: its distribution obligations are carried out here.
  - ADR-0048: the proxy image overrides its base's labels.
  - ADR-0022: `scripts/checks/tests/azurite-pin.test.ts` becomes `image-pins.test.ts`, which guards the BuildKit pin as well.

## Options rejected

- **An image vulnerability scanner** (Grype, Trivy, Docker Scout, Defender for Cloud). The owner chose the Dependabot gate. The gap stays recorded: no Dependabot alert names an operating-system package of a base layer, so a known hole there waits for the next base image update that Dependabot proposes and the gate makes a release wait for.
- **Private images and a GitHub token on the server.** A classic token with `read:packages` would also read the company's other private packages if it leaked, and the owner chose public images.
- **Verifying through GitHub's API with a token on the server.** It notices a renamed repository by itself, but leaves a long-lived credential on an account that is root through Docker. Offline verification with the repository's id check gives the same assurance.
- **BuildKit's SBOM or Microsoft's sbom-tool.** The first needs a scanner image the owner has not approved. The second leaves out every application package and runs a container with the Docker socket in the job that holds the signing token.
- **Deleting `libmsalruntime.so` after publishing.** `deps.json` still lists it, so the host reports a missing asset, and a file deleted in a later layer still ships in a lower one.
- **Shipping libvips with its notices.** It would bring MPL-1.1 code into the image, outside the licence policy, for a feature the web app does not use.
- **`up -d --wait` without `--no-deps`.** It runs the migrator again after the old api is gone, so a passing failure there becomes an outage.
- **Rolling back automatically across a migration.** The older API does not support the newer schema. The runbook's forward fix or point-in-time restore is the way back.
- **BuildKit pinned in the workflows.** No Dependabot ecosystem reads an input of a workflow step, so the pin would fall behind BuildKit's security releases without a pull request to hold a release for it.
- **A switch that skips verification for tests.** The decisions of both scripts are tested with stand-in commands instead, and the release's self-check proves the verification against real signatures.

## Consequences

- **A release waits for Dependabot.** Every Dependabot pull request, base image and BuildKit updates included, holds a release until it is merged or closed, and the release is then a tag on main's newest commit.
- **Main's checks must be finished.** A release is cut only after main's full Playwright matrix finished; the gate refuses while any check run of the commit is unfinished, GitHub's own `submit-nuget` and Dependabot's runs included, or while the newest run of any check failed, and a re-run of the gate picks them up once they pass, as long as main has not moved, except a failed refresh of one Dependabot pull request, which only a later passing run with its title on that commit clears.
- **Notices follow every dependency.**
  - Every NuGet, npm or base image change regenerates them (`node scripts/notices/generate.ts`), or `docker-build` fails on the drift.
  - A Dependabot pull request that moves one is therefore never green on its own: it is adopted in an owner-authored pull request that carries the regenerated notices, as the code-owner review already requires.
  - A Caddy version also needs the SHA-256 of Caddy's buildable artifact in `caddyArtifacts` (`scripts/notices/lib/go.ts`), which reads the Go modules' licence texts from it.
- **The packages are made public by hand.** GitHub creates a package private and has no REST endpoint for its visibility, so the first release stops at the self-check until the three packages are set public in their settings, then its failed jobs are re-run (deploy runbook, "The first release").
- **A failed release leaves packages behind.** It can leave pushed but unattested package versions. `deploy.sh` refuses them, and deleting them is the owner's decision.
- **The first deployment needs a manual step.** The server's checkout predates `deploy.sh`, so the first deployment moves it to the release by hand. A release that changes `infra/host` or `bootstrap.sh` needs the administrator to run the bootstrap again from that tag, which `deploy.sh` warns about.
- **The legal question is open.** Whether the notices and source locations meet the GPL duties of the base images' operating-system packages is the lawyer's to confirm. The strict alternative is attaching their source archives to each release.
