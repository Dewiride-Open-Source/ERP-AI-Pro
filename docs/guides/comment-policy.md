# Comment policy

Comments describe the current code only. The policy is enforced by review and by `scripts/checks/comment-policy.ts` (part of `node scripts/verify/verify.ts`).

## The three tests

1. **Sibling rule** — look at the members of the same type (or the top-level declarations of the same file). If none carries a comment, add none. If all carry documentation comments, add one of the same shape. Mixed: add none.
2. **Information test** — delete the comment. If the identifier, type, signature or test name already says it, it stays deleted.
3. **Current-state test** — the comment must be true of the code as it is now, with no reference to how it was or how it changed.

## Allowed

- A *why*: a constraint or trade-off that the code cannot express.
- An invariant the reader must not break.
- A statutory citation: `CGST Rule 46(1)(b)`, `IGST Act s.13(2)`.
- A reference to a non-obvious algorithm or specification.

## Banned

- What the code does (`// increment the counter`).
- Change history: "moved from", "previously", "no longer", "replaces", "updated to", "legacy", "deprecated", "old", "new" used historically.
- TODO, FIXME, HACK, XXX — use roadmap notes (`node scripts/roadmap/roadmap.ts note <id> "..."`) instead.
- Ticket ids, author names, dates.
- Suppressions (`#pragma`, `eslint-disable`, `NoWarn`) without an on-line reason.
- Commented-out code, licence or file headers, `#region`, separator lines.

## Documentation comments

XML documentation comments are never mandatory. They are used only where they carry information into OpenAPI (endpoint handler methods, request and response records) or explain a contract's semantics that its name cannot. CS1591 is suppressed repository-wide.

## Exempt files

Generated files: EF Core migrations, shadcn `components/ui`, generated API clients, `docs/roadmap/ROADMAP.md`, lockfiles.
