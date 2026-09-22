# ADR-0014: The `cn` package replaces `clsx` and `tailwind-merge`

Status: accepted
Date: 2026-09-22

## Context

ADR-0009 approved `clsx` and `tailwind-merge` as shadcn runtime dependencies; `packages/ui/src/lib/utils.ts` composed them into the `cn` helper every component imports. The shadcn registry (CLI 4.21) now emits `import { cn } from "cn"` in every component it generates and lists the npm package `cn` as a registry dependency: `shadcn add` installs it and the generated file does not compile against the local helper without a hand edit, which the rule "primitives change only through `shadcn add`/`shadcn diff`" forbids. `cn` (MIT, github.com/shadcn-ui/cn, maintained by shadcn, no runtime dependencies) is the same author's drop-in replacement for `clsx` + `tailwind-merge`, and `shadcn migrate cn` rewrites a project to it.

## Decision

- `cn` is approved (owner decision of 2026-09-22) and pinned in the pnpm catalog; `clsx` and `tailwind-merge` leave the approved set and the workspace.
- The design system is migrated with `shadcn migrate cn`: `packages/ui/src/lib/utils.ts` re-exports `cn` from the package so the `aliases.utils` entry of `components.json` and every existing import stay valid, and every component the CLI generates imports `cn` from the package directly. Both import shapes are the CLI's own output and neither is hand-edited.
- The Dependabot `ui` group covers `cn`; the catalog pin follows `minimumReleaseAge`, so a release younger than 24 hours waits.

## Consequences

- `shadcn add` and `shadcn diff` produce compilable files without manual edits.
- Every approval remains traceable: ADR-0009 for the rest of the set, this record for `cn`.
