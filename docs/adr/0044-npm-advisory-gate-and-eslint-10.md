# ADR-0044: The npm advisory gate and ESLint 10 through @eslint/compat

Status: accepted
Date: 2026-10-17

## Context

- **No npm advisory reaches GitHub's alerts.**
  - The dependency graph lists this workspace's npm packages only as `pkg:npm/<name>@catalog:`. It has no versions and no transitive packages (`docs/operations/github-repository-settings.md`), so neither Dependabot alerts nor dependency review can match an npm advisory.
  - ADR-0030 closed the licence half of that gap with `scripts/checks/licences.ts`. Advisories were still found only by hand.
  - In the dependency round of 2026-10-17 (#86), `pnpm audit` reported four high advisories in packages the workspace reaches indirectly:

    | Package | Advisory | Outcome |
    |---|---|---|
    | source-map-js 1.2.1 | GHSA-68fv-2mgg-jv7q | fixed in #86 |
    | sharp 0.35.4 | GHSA-wq5f-xc86-pv6w | fixed in #86 |
    | `@modelcontextprotocol/sdk` 1.30.0 | GHSA-6qxp-vccf-f47h | fixed in #86 |
    | braces 3.0.3 | GHSA-vfj7-8cjw-p6xm | no release outside the vulnerable range `<=3.0.3` exists |

  - The Next.js and React advisories of 2026-10-14 were not yet in the global advisory database on 2026-10-17. They were read in the vercel/next.js and facebook/react repository advisories.
- **ESLint 9 is out of support.**
  - ESLint 9 reached end of life on 2026-08-06 (eslint.org/version-support) and gets no further release, security fixes included.
  - npm marks 9.39.5 "no longer supported", and the package policy fixes a deprecated package by upgrading it.
  - `eslint-config-next` (16.4.1, and also 16.4.2 and 16.5.0-canary.16) bundles three plugins whose peer ranges stop at ESLint 9: `eslint-plugin-react` 7.37.5, `eslint-plugin-import` 2.32.0 and `eslint-plugin-jsx-a11y` 6.10.2.
  - The first two call methods that ESLint 10 removed, without a fallback. They do it in rules and code paths the presets do not reach today:

    | Plugin | Rule or code path | Removed method |
    |---|---|---|
    | eslint-plugin-react | `forward-ref-uses-ref` | `getSourceCode()` |
    | eslint-plugin-react | `jsx-filename-extension` | `getFilename()` |
    | eslint-plugin-react | the JSX spacing rules | `isSpaceBetweenTokens()` |
    | eslint-plugin-import | `import/order` | `getTokenOrCommentBefore()` and `getTokenOrCommentAfter()` |
    | eslint-plugin-import | the export map | the `parserOptions` and `parserPath` properties |

  - The ESLint team's `@eslint/compat` (Apache-2.0) wraps rules written for ESLint 8 or 9 so that they run on 10.
- **The owner decided both on 2026-10-17:**
  - an automatic advisory check on every pull request and once a day. It fails on high and critical advisories, with reviewed exceptions that fail again once a fix is released;
  - ESLint 10 with `@eslint/compat`, which the owner approved. ESLint stays on 9 if a rule still breaks.

## Decision

- **The advisory gate.** `node scripts/checks/advisories.ts` runs `pnpm audit --json` over `frontend/pnpm-lock.yaml`, with every dependency included. It reads pnpm 12's report in `scripts/checks/lib/advisory-policy.ts` and fails when:
  - a `high` or `critical` advisory is not listed in `reviewedAdvisories`. The finding names the fixed version when one is published;
  - pnpm reports a fixed version for a listed advisory, so an exception ends when its fix ships. pnpm infers the fixed range from the vulnerable range and keeps it only when a published, non-deprecated release satisfies it. Otherwise it reports `patched_versions: null` with `patched_versions_unpublished: true`;
  - a listed advisory is reached through a direct dependency of a workspace project that its review does not name in `through`;
  - a listed advisory is no longer reported as high or critical, so the list stays true.

  `info`, `low` and `moderate` advisories are printed and never fail. The exit code of `pnpm audit` is not used, because it is non-zero whenever an advisory is reported. A run that prints no report fails.
- **Where it runs:**
  - in ci-frontend after the npm licences, inside the required "Lint, typecheck and build" job, on every pull request and every push to `main`;
  - in `verify.ts` after the npm licences;
  - daily from `.github/workflows/advisories.yml`, at 00:17 UTC or by `workflow_dispatch`. An advisory published between changes fails that run, and GitHub notifies the person who last changed the workflow's `cron` line.
- **The one exception today** is braces GHSA-vfj7-8cjw-p6xm: high, with no fixed release.
  - It is reached only through `fast-glob` and `micromatch`, used by `@next/eslint-plugin-next` (from `eslint-config-next`) and by the `shadcn` command line.
  - Both are development tools that expand the repository's own file patterns. They never run in the web app, whose image copies only the traced standalone output.
- **ESLint 10.12.0 with `@eslint/compat` 2.1.1.** 10.12.0 is the newest release outside the three-day cooldown; 10.13.0 came out on 2026-10-16.
  - The `next` and `react-library` presets of `@dewiride/erp-config` pass every configuration object of `eslint-config-next/core-web-vitals` and `eslint-config-next/typescript` through `fixupConfigRules`. Its cache gives each plugin one wrapped object, so the two lists never redefine a plugin.
  - The `node` preset uses only the TypeScript rules and is unchanged.
  - `pnpm-workspace.yaml` allows ESLint 10 as the peer of the three plugins (`peerDependencyRules.allowedVersions`), so the install reports a peer issue only when a new one appears.
  - `eslint/next-rule-sets.test.mjs` proves that both presets still report a missing `key`, an image without alternative text, an anonymous default export and a conditional hook.
  - Dependabot no longer holds back ESLint majors, and its `tooling` group includes `@eslint/*`.
- **When the wrapper ends.** Once `eslint-config-next` bundles plugins that support ESLint 10, the presets spread its lists directly again, and `@eslint/compat` and the three peer rules leave the workspace.

## Consequences

- **Advisories now block changes.**
  - A high or critical advisory in any npm package of the workspace stops every pull request until it is fixed or reviewed.
  - The daily run reports an advisory published between pull requests.
  - An advisory first published as a repository advisory reaches the global database, and so this check, only once GitHub reviews it. The Next.js and React repository advisories are therefore still read in every dependency round.
- **An exception has a cost.** It needs a reviewed entry with its reason and the dependencies it is reached through, and it fails as soon as a fixed release exists or the package is reached another way.
- **A registry lookup can fail.** When pnpm cannot read a package's registry entry, it keeps the fixed range it inferred. A reviewed advisory can then fail once although no fix exists; the next run settles it.
- **The daily workflow can be disabled.** GitHub disables a scheduled workflow of a public repository after 60 days without activity. It is enabled again from the Actions tab.
- **The lint runs on a supported ESLint.** It keeps every rule `eslint-config-next` enables.
  - ESLint 10 counts JSX names as references and adds three rules to `eslint:recommended`. No preset here extends `eslint:recommended`.
  - `pnpm lint` passes on the whole workspace without a change to any source file.
- **Earlier decisions this changes:**
  - ADR-0030 made `scripts/checks/licences.ts` the npm gate that GitHub cannot provide. Advisories now have a gate of their own.
  - The approved packages of ADR-0009 gain `@eslint/compat`.
