# GitHub repository settings

These settings live outside the repository, in **Settings** of `Dewiride-Open-Source/ERP-AI-Pro`, and every one of them is in force except where an item says otherwise. Claude applies and maintains them through the GitHub API, asking the owner's permission before each change (root `CLAUDE.md` §16), and keeps this list equal to what GitHub reports. Everything else (workflows, Dependabot, CodeQL configuration, templates) is versioned in `.github/`.

## Actions

- **Actions → General → Workflow permissions**: *Read repository contents and packages permissions*; leave *Allow GitHub Actions to create and approve pull requests* unchecked.
- **Actions → General → Actions permissions**: *Allow all actions*, with **Require actions to be pinned to a full-length commit SHA** (`sha_pinning_required`; every workflow already pins its actions).

## Code security

- **Dependency graph** and **Dependabot alerts**: enabled (automatic for public repositories). Keep the alerts enabled: the `gate` job of `release.yml` reads them with the `vulnerability-alerts: read` permission and refuses every release while the alerts API answers 403.
  - Dependency review (the dependency-review workflow) reads `.github/dependency-review-config.yml`: it fails a pull request on a high-severity vulnerability or on a dependency whose licence is outside the allow list of `scripts/checks/lib/licence-policy.ts`, which `scripts/checks/tests/licences.test.ts` keeps equal to the configuration ([ADR-0030](../adr/0030-community-and-enterprise-editions.md)). The dependency graph does not see this workspace's transitive npm packages (it lists npm packages only as `pkg:npm/<name>@catalog:`, without licences), so `scripts/checks/licences.ts` is the gate for them: `node scripts/checks/licences.ts npm` checks every installed npm package (ci-frontend) and `node scripts/checks/licences.ts nuget` every NuGet package restored for any project of the solution (the `libraries` of every `backend/artifacts/obj/*/project.assets.json`) and every local tool of `backend/.config/dotnet-tools.json` (ci-backend); a reviewed package passes only while its pinned licence evidence (declared npm text, NuGet licence-file SHA-256 or NuGet licence URL) is unchanged.
  - Without versions, Dependabot alerts and dependency review match no npm advisory either, so `scripts/checks/advisories.ts` is the advisory gate for the frontend workspace ([ADR-0044](../adr/0044-npm-advisory-gate-and-eslint-10.md)).
    - **What it checks:** `node scripts/checks/advisories.ts` runs `pnpm audit --json` over `frontend/pnpm-lock.yaml`. It fails on a high or critical advisory that `reviewedAdvisories` of `scripts/checks/lib/advisory-policy.ts` does not list. A listed advisory fails once a fixed release is published, when it is reached through a dependency its review does not name, or when it is no longer reported.
    - **Where it runs:** in ci-frontend, and daily from `.github/workflows/advisories.yml` at 00:17 UTC. GitHub notifies the person who last changed that workflow's `cron` line when the daily run fails.
    - **Scheduled runs pause:** in a public repository, GitHub disables scheduled workflows after 60 days without activity; they are enabled again from the Actions tab.
- **Dependabot security updates**: enabled. Version updates come from `.github/dependabot.yml`.
- **Code scanning**: do **not** enable *Default setup*. The repository ships the advanced-setup workflow `.github/workflows/codeql.yml` (C#, JavaScript/TypeScript, Actions); enabling default setup would reject its uploads.
- **Secret scanning** and **Push protection**: enabled.
  - The in-repo check `node scripts/checks/secret-patterns.ts` (verify.ts and the ci-backend workflow) covers the same ground for every tracked and new file, so a value that GitHub does not recognise still fails the build; a push-protection block is fixed by rotating the value ([runbooks/secrets.md](runbooks/secrets.md)), never bypassed.
- **Private vulnerability reporting**: enabled (referenced by `SECURITY.md` and the issue template config).

## Rulesets → `main`

The ruleset is named `main` and targets the default branch.


- Restrict deletions.
- Require linear history.
- Require a pull request before merging; squash merge only; dismiss stale approvals.
- Require status checks to pass, each check required from the GitHub Actions app (integration 15368) so no other app can report it. GitHub lists checks by **job name**, and every workflow runs on each pull request (no path filters), so all of these always report:
  - `Build, analyse and test` (ci-backend)
  - `Lint, typecheck and build` (ci-frontend)
  - `Playwright` (e2e: the job that merges the shards' blob reports into one HTML report and fails unless every shard passed; a pull request shards chromium and mobile-android, `main` all six projects, [ADR-0028](../adr/0028-playwright-matrix-navigation-crawl-and-sharded-reports.md))
  - `Build images and smoke test the compose stack` (docker-build)
  - `Bootstrap a fresh Ubuntu host (ubuntu-22.04)`, `(ubuntu-24.04)` and `(ubuntu-26.04)` (docker-build: `scripts/deploy/bootstrap.sh` on a GitHub-hosted runner of each supported release, [ADR-0049](../adr/0049-the-production-host-a-deploy-account-keys-from-the-home-network-and-nightly-updates.md); owner decision of 2026-10-18)
  - `Review dependency changes` (dependency-review)
  - `Validate roadmap and rendered Markdown` (roadmap)
  - `Contributor licence agreement` (cla; it runs on `pull_request_target`, so the workflow, the checker and the exemptions always come from `main`, and it reports from the first pull request after the workflow reaches `main`, [ADR-0030](../adr/0030-community-and-enterprise-editions.md))
  - `Analyze (csharp)`, `Analyze (javascript-typescript)`, `Analyze (actions)` (CodeQL)
- Require review from Code Owners, with the Repository admin role as a bypass actor in "For pull requests only" mode. `.github/CODEOWNERS` covers `/.github/` and `/docs/cla/`, and a required check is matched by job name, so an outside or Dependabot pull request that adds a workflow with a job named like a required check (or edits a signature file) waits for the owner's approval; GitHub never lets an author approve their own pull request, so a pull request opened with the owner's account is merged through the bypass (`gh pr merge --admin`, or the REST merge endpoint), only once every required check has passed and with the owner's permission, and the bypass never allows a direct push. Where the organisation's plan offers "Require workflows to pass before merging", require `.github/workflows/cla.yml` from `main` that way as well.
- Block force pushes.
- Signed commits are not required yet; the rule is added, with the owner's permission, once SSH commit signing is configured on the owner's machine (see `docs/guides/local-development.md`).

## Rulesets → tags `v*`

- The ruleset `release tags` restricts creation, update and deletion of `refs/tags/v*` to the Repository admin role (bypass mode *always*), because release tags trigger image publishing from the first-deployment phase.

## General

- Default branch `main`; merge button: squash only, with the pull request title as the commit title and the commit messages as its body; automatically delete head branches.
- Issues enabled with the templates in `.github/ISSUE_TEMPLATE/`; Discussions optional.
- Packages: see [Releases](#releases).

## Releases

`.github/workflows/release.yml` runs on a pushed tag `v*` and publishes the release `ERP-AI-Pro <tag>` from a draft ([ADR-0053](../adr/0053-the-release-public-images-a-dependabot-gate-attestations-verified-on-the-server-and-the-deployment-scripts.md), [deploy runbook](runbooks/deploy.md)). Its jobs hold only these permissions: `gate` reads `contents`, `checks`, `actions`, `pull-requests` and `vulnerability-alerts`; `build` reads `contents` and writes `packages`; `attest` reads `contents` and writes `packages`, `id-token` and `attestations`; `release` writes `contents`.

- **Required checks of a release.** The gate requires the 23 check runs that run on a push to `main`, by job name. Renaming or adding a job of `ci-backend`, `ci-frontend`, `docker-build`, `e2e`, `codeql` or `roadmap` changes the `required` list of `release.yml` in the same commit, or every release is refused.
- **Organisation → Packages → Package creation**: members may create public packages, so the first release's `GITHUB_TOKEN` creates `ghcr.io/dewiride-open-source/erp-ai-pro/api`, `web` and `proxy`. Each package is then linked to this repository and set to **Public** (owner decision of 2026-10-19), and keeps that visibility: a public package can never be made private again.
- **Immutable releases** (Settings → General → Releases): enabled (owner decision of 2026-10-28), so a published release's tag and files can no longer change.

## Labels

- Labels `dependencies`, `backend`, `frontend`, `ci` and `infra` exist (`gh label create <name>`): `.github/dependabot.yml` applies them to every update pull request, and Dependabot posts a comment instead of labelling when one is missing.
