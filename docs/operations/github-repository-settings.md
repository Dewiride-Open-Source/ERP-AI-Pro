# GitHub repository settings (owner checklist)

These settings live outside the repository and must be applied by the owner in **Settings** of `Dewiride-Open-Source/ERP-AI-Pro`. Everything else (workflows, Dependabot, CodeQL configuration, templates) is versioned in `.github/`.

## Actions

- [ ] **Actions → General → Workflow permissions**: *Read repository contents and packages permissions*; leave *Allow GitHub Actions to create and approve pull requests* unchecked.
- [ ] **Actions → General → Actions permissions**: *Allow enterprise, and select non-enterprise, actions and reusable workflows* is not needed; keep *Allow all actions* but enable **Require actions to be pinned to a full-length commit SHA** (every workflow already does this).

## Code security

- [ ] **Dependency graph** and **Dependabot alerts**: enabled (automatic for public repositories).
  - Dependency review (the dependency-review workflow) reads `.github/dependency-review-config.yml`: it fails a pull request on a high-severity vulnerability or on a dependency whose licence is outside the allow list of `scripts/checks/lib/licence-policy.ts`, which `scripts/checks/tests/licences.test.ts` keeps equal to the configuration ([ADR-0030](../adr/0030-community-and-enterprise-editions.md)). The dependency graph does not see this workspace's transitive npm packages (it lists npm packages only as `pkg:npm/<name>@catalog:`, without licences), so `scripts/checks/licences.ts` is the gate for them: `node scripts/checks/licences.ts npm` checks every installed npm package (ci-frontend) and `node scripts/checks/licences.ts nuget` every NuGet package restored for any project of the solution (the `libraries` of every `backend/artifacts/obj/*/project.assets.json`) and every local tool of `backend/.config/dotnet-tools.json` (ci-backend); a reviewed package passes only while its pinned licence evidence (declared npm text, NuGet licence-file SHA-256 or NuGet licence URL) is unchanged.
- [ ] **Dependabot security updates**: enabled. Version updates come from `.github/dependabot.yml`.
- [ ] **Code scanning**: do **not** enable *Default setup*. The repository ships the advanced-setup workflow `.github/workflows/codeql.yml` (C#, JavaScript/TypeScript, Actions); enabling default setup would reject its uploads.
- [ ] **Secret scanning** and **Push protection**: enabled.
  - The in-repo check `node scripts/checks/secret-patterns.ts` (verify.ts and the ci-backend workflow) covers the same ground for every tracked and new file, so a value that GitHub does not recognise still fails the build; a push-protection block is fixed by rotating the value ([runbooks/secrets.md](runbooks/secrets.md)), never bypassed.
- [ ] **Private vulnerability reporting**: enabled (referenced by `SECURITY.md` and the issue template config).

## Rulesets → `main`

- [ ] Restrict deletions.
- [ ] Require linear history.
- [ ] Require a pull request before merging; squash merge only; dismiss stale approvals.
- [ ] Require status checks to pass. GitHub lists checks by **job name**, and every workflow runs on each pull request (no path filters), so all of these always report:
  - `Build, analyse and test` (ci-backend)
  - `Lint, typecheck and build` (ci-frontend)
  - `Playwright` (e2e: the job that merges the shards' blob reports into one HTML report and fails unless every shard passed; a pull request shards chromium and mobile-android, `main` all six projects, [ADR-0028](../adr/0028-playwright-matrix-navigation-crawl-and-sharded-reports.md))
  - `Build images and smoke test the compose stack` (docker-build)
  - `Review dependency changes` (dependency-review)
  - `Validate roadmap and rendered Markdown` (roadmap)
  - `Contributor licence agreement` (cla; it runs on `pull_request_target`, so the workflow, the checker and the exemptions always come from `main`, and it reports from the first pull request after the workflow reaches `main`, [ADR-0030](../adr/0030-community-and-enterprise-editions.md))
  - `Analyze (csharp)`, `Analyze (javascript-typescript)`, `Analyze (actions)` (CodeQL)
- [ ] Require review from Code Owners, with the Repository admin role as a bypass actor in "For pull requests only" mode. `.github/CODEOWNERS` covers `/.github/` and `/docs/cla/`, and a required check is matched by job name, so an outside or Dependabot pull request that adds a workflow with a job named like a required check (or edits a signature file) waits for the owner's approval; GitHub never lets an author approve their own pull request, so the owner merges their own pull requests through the bypass, which never allows a direct push. Where the organisation's plan offers "Require workflows to pass before merging", require `.github/workflows/cla.yml` from `main` that way as well.
- [ ] Block force pushes.
- [ ] Require signed commits once SSH commit signing is configured on the owner's machine (see `docs/guides/local-development.md`).

## Rulesets → tags `v*`

- [ ] Restrict creation and deletion to repository administrators (release tags trigger image publishing from the first-deployment phase).

## General

- [ ] Default branch `main`; merge button: squash only; automatically delete head branches.
- [ ] Issues enabled with the templates in `.github/ISSUE_TEMPLATE/`; Discussions optional.
- [ ] Packages: GitHub Container Registry visibility for `ghcr.io/dewiride-open-source/erp-ai-pro/*` is set when the first image is published.

## Labels

- [ ] Labels `dependencies`, `backend`, `frontend`, `ci` and `infra` exist (`gh label create <name>`): `.github/dependabot.yml` applies them to every update pull request, and Dependabot posts a comment instead of labelling when one is missing.
