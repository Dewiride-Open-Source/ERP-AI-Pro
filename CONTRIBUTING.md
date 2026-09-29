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
- No third-party package outside the approved list in `docs/adr/0009-third-party-packages.md` without owner approval,
  and no dependency whose licence is missing from the allow list in `scripts/checks/lib/licence-policy.ts`.
- No secrets, connection strings or statutory numbers hard-coded anywhere.
- Every folder stays under the file cap; every new page and control has a Playwright spec.
- Every commit author and co-author is exempt or has signed the contributor licence agreement.

## Licence of contributions

This repository is licensed under the GNU Lesser General Public License version 3 only (`LGPL-3.0-only`):
`COPYING.LESSER` holds the LGPL-3.0 text and `COPYING` the GPL-3.0 text it builds on. Every contribution is
licensed under the same terms. Dewiride Technologies Private Limited, the copyright holder, also builds a
proprietary Enterprise edition on this code.

## Contributor licence agreement

Before a pull request can merge, everyone whose commits it contains has signed a contributor licence agreement
with Dewiride Technologies Private Limited: the individual agreement (`docs/cla/individual-cla-1.0.md`) when you
contribute for yourself, or the corporate agreement (`docs/cla/corporate-cla-1.0.md`) when a company owns what you
write. You sign by adding one file in your first pull request, as `docs/cla/sign-cla.md` describes.

- You keep the copyright in your contribution.
- Dewiride Technologies Private Limited may also license your contribution under other terms, including in the
  Enterprise edition, but always also under `LGPL-3.0-only`.
- The `Contributor licence agreement` check runs on every pull request and fails until every commit author and
  every `Co-authored-by` email is exempt (`docs/cla/exempt.json`) or covered by a signature.

## Editions

This repository is the ERP-AI-Pro Community edition. Code here implements Community roadmap items only and never
names, imports or stubs an Enterprise module; the Enterprise edition builds on this repository, never the reverse.
See `docs/adr/0030-community-and-enterprise-editions.md`.

## Local setup

See `docs/guides/local-development.md`.
