# Contributing

Read `docs/architecture/` first (overview, module anatomy, dependency rules, naming) and `docs/guides/`
(local development, testing, comment policy). This file covers the workflow around a change.

## Workflow

1. Every change belongs to a roadmap sub-phase in `docs/roadmap/roadmap.json`. Start it with
   `node scripts/roadmap/roadmap.ts start <sub-phase-id>`.
2. Branch from `main`: `<type>/<sub-phase-id>[-<slug>]`, e.g. `feat/authentication-oidc-bff-in-the-api`.
3. Implement to the Definition of Done in `.github/PULL_REQUEST_TEMPLATE.md`, including tests and Playwright coverage.
4. Run `node scripts/verify/verify.ts` locally. If more than 20 files changed, also run the verification
   review workflow and record the result in the pull request.
5. Mark the sub-phase done (`node scripts/roadmap/roadmap.ts done <sub-phase-id>`), which regenerates
   `docs/roadmap/ROADMAP.md`.
6. Open a pull request. Pull requests are squash-merged; the title is the Conventional Commit subject.

## Commit messages

Conventional Commits: `type(scope): imperative subject` with the roadmap sub-phase id in the body.

- Types: `feat`, `fix`, `refactor`, `test`, `docs`, `build`, `ci`, `chore`, `perf`, `security`.
- Scopes: `build`, `ci`, `infra`, `host`, `web`, `e2e`, `roadmap`, `docs`, or the module in kebab-case
  (`finance-sales`, `user-management`, `platform-system-info`).
- Subject ≤ 72 characters; body explains why.

## Rules that block a merge

- Comments describe the current code only; no change history, no TODO/FIXME, no commented-out code.
- No backward-compatibility shims, `[Obsolete]` members, duplicate "v2" code paths or unused columns.
- No third-party package outside the approved list in `docs/adr/0009-third-party-packages.md` without owner approval.
- No secrets, connection strings or statutory numbers hard-coded anywhere.
- Every folder stays under the file cap; every new page and control has a Playwright spec.

## Local setup

See `docs/guides/local-development.md`.
