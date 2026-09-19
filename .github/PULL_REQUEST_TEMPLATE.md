## Roadmap item

`<sub-phase-id>` — <one line: what now exists>

## What and why

-

## Verification

- [ ] `node scripts/verify/verify.ts` passed locally
- [ ] Unit / integration / architecture tests added or updated
- [ ] Playwright spec covers every new page and control (desktop + mobile, light + dark) — screenshots attached or linked
- [ ] More than 20 files changed → verification review workflow ran; result:
- [ ] Migrations added for affected modules; `has-pending-model-changes` clean (from P03)
- [ ] New configuration keys / feature flags documented in `docs/configuration.md`; new routes in `docs/routing.md`

## Rules

- [ ] Comment policy respected (current code only; no history, no TODO/FIXME)
- [ ] No backward-compatibility shims; dead code deleted
- [ ] No unapproved third-party packages; no secrets; no hard-coded statutory numbers
- [ ] `docs/roadmap/roadmap.json` updated through the CLI and `ROADMAP.md` regenerated
