# GitHub repository settings (owner checklist)

These settings live outside the repository and must be applied by the owner in **Settings** of `Dewiride-Open-Source/ERP-AI-Pro`. Everything else (workflows, Dependabot, CodeQL configuration, templates) is versioned in `.github/`.

## Actions

- [ ] **Actions → General → Workflow permissions**: *Read repository contents and packages permissions*; leave *Allow GitHub Actions to create and approve pull requests* unchecked.
- [ ] **Actions → General → Actions permissions**: *Allow enterprise, and select non-enterprise, actions and reusable workflows* is not needed; keep *Allow all actions* but enable **Require actions to be pinned to a full-length commit SHA** (every workflow already does this).

## Code security

- [ ] **Dependency graph** and **Dependabot alerts**: enabled (automatic for public repositories).
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
  - `Playwright (chromium)` and `Playwright (mobile-android)` (e2e; the other three projects run on `main` only)
  - `Build images and smoke test the compose stack` (docker-build)
  - `Review dependency changes` (dependency-review)
  - `Validate roadmap and rendered Markdown` (roadmap)
  - `Analyze (csharp)`, `Analyze (javascript-typescript)`, `Analyze (actions)` (CodeQL)
- [ ] Block force pushes.
- [ ] Require signed commits once SSH commit signing is configured on the owner's machine (see `docs/guides/local-development.md`).

## Rulesets → tags `v*`

- [ ] Restrict creation and deletion to repository administrators (release tags trigger image publishing from the first-deployment phase).

## General

- [ ] Default branch `main`; merge button: squash only; automatically delete head branches.
- [ ] Issues enabled with the templates in `.github/ISSUE_TEMPLATE/`; Discussions optional.
- [ ] Packages: GitHub Container Registry visibility for `ghcr.io/dewiride-open-source/erp-ai-pro/*` is set when the first image is published.
