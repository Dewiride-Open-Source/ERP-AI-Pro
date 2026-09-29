# ADR-0028: Playwright matrix, navigation crawl and sharded reports

Status: accepted
Date: 2026-09-29

## Context

The `web-foundation-e2e-harness-matrix` sub-phase asks for per-viewport Playwright projects crossed with light and dark, a screenshot naming convention, a navigation crawl that visits every registered navigation entry and asserts zero console errors, a blob reporter with sharding and merging in CI, and HTML report artifacts, with the signed-in session left to `authentication-test-authentication-infrastructure`. Facts that shape the decision:

- **The suite today.** `frontend/e2e/playwright.config.ts` defines five projects: desktop Chromium, Firefox and WebKit, `Pixel 10` as `mobile-android` and `iPhone 17` as `mobile-ios`. `forEachTheme` runs a visual test once in light and once in dark, while behavioural tests run once. `capture(name, target?)` names every screenshot `<spec>/<name>--<project>--<theme>.png` and scans the screen with axe (ADR-0027). `e2e.yml` runs one job per project (`Playwright (<project>)`): chromium and mobile-android on a pull request, all five on `main`. Each job uploads its own HTML report.
- **Timing.** A local run of the five projects takes about 30 minutes with two workers, and a CI job needs about seven minutes of setup (SQL Server, Azurite, backend and web builds, browsers) before its tests start. The chromium project alone runs about six minutes of tests on a runner.
- **Playwright 1.63.** `--shard=x/y` runs a fraction of the suite. The `blob` reporter writes a zip with every result and attachment, named by `PLAYWRIGHT_BLOB_OUTPUT_NAME` or else `report-<hash>[-<shard>].zip`, and `playwright merge-reports --reporter html <dir>` merges blobs from one operating system into one report (https://playwright.dev/docs/test-sharding, https://playwright.dev/docs/test-reporters). The newest tablet descriptors are `iPad (gen 11)` (WebKit, 656 × 944, touch) and `Galaxy Tab S9` (Chromium, 640 × 1024).
- **The owner's decision (2026-09-29).** Add Playwright's newest iPad to the full run on `main`; pull requests keep chromium and one mobile project.
- **The shell.** It renders the enabled entries of the module registry (`features/registry.ts`, each module's `nav.ts`) as the links of its "Primary" navigation. That navigation is hidden below the `sm` breakpoint until the authentication phase adds the drawer. No test visited every entry.

## Decision

- **The matrix** is three viewports on six projects:
  - desktop: `chromium` (`Desktop Chrome`), `firefox` (`Desktop Firefox`), `webkit` (`Desktop Safari`);
  - tablet: `tablet-ios` (`iPad (gen 11)`);
  - phone: `mobile-android` (`Pixel 10`), `mobile-ios` (`iPhone 17`).

  Each visual test crosses them with light and dark through `forEachTheme`. Themes stay a test-level loop rather than twelve projects, so behavioural tests keep running once per project. `tests/smoke/**` stays on Chromium only.
- **Naming** stays `<spec>/<name>--<project>--<theme>.png` under `frontend/e2e/screenshots`, with `--repeat<N>` for the copies under `--repeat-each`. Every capture is attached to the report under `<name>--<theme>`.
- **The navigation crawl.**
  - `fixtures/navigation.ts` provides `registeredNavigationEntries(page)`, which reads the name and address of every link in the "Primary" navigation, hidden ones included.
  - `tests/shared/layout/navigation-crawl.spec.ts` runs in both themes on every project. It starts on `/design/form-kit`, reads the entries and visits each one. For each, it asserts a 200, no loading status left, a visible `h1` in `main` and no not-found heading, captures the page (and so scans it) and asserts no console error. Each failure names the page.
  - `ThemedFixtures` now carries `consoleErrors`, so a test can check them per page.
  - The time budget is 15 s plus 45 s per entry, so it grows with the registry.
- **Sharding and one report.**
  - `e2e.yml` runs each project in two shards: the job `Playwright (<project>, shard <n> of 2)` runs `--project=<project> --shard=<n>/2` with `PLAYWRIGHT_BLOB_OUTPUT_NAME=report-<project>-<n>.zip`.
  - In CI the config's reporters are `list`, `blob` and `github`.
  - Each shard uploads `blob-report-<project>-<n>` and its screenshots and failure evidence (`playwright-<project>-<n>`), plus its server logs on failure.
  - The job `Playwright` then runs even when a shard failed (`!cancelled()`). It downloads every blob report, merges them into the HTML report artifact `playwright-report`, and fails unless every shard succeeded.
  - The repository ruleset therefore requires the single check `Playwright`, whatever the number of projects and shards.
- **The signed-in session** is not built here: `authentication-test-authentication-infrastructure` adds the fixture that signs a browser in, and every page is anonymous until then.

## Consequences

- A pull request's browser run takes two parallel jobs per project instead of one, and finishes about three minutes sooner for the chromium project. `main` runs twelve shard jobs and the report job.
- A new module's navigation entry is crawled, scanned and screenshotted on every project without a new test. A page that only a direct link reaches (the design harnesses) stays covered by its own spec.
- The iPad reports `isMobile` and touch, so specs treat it as a touch device. Its 656 px viewport shows the primary navigation (`sm` is 640 px) and, where a container is narrower than 42 rem, the data table's cards.
- **Earlier decisions this changes.** No earlier ADR records the matrix or the jobs. The five projects and one job per project lived in the project instructions (root `CLAUDE.md` section 11) and in `docs/guides/testing.md`; both now name six projects and the sharded jobs. `docs/operations/github-repository-settings.md` now names the single required check `Playwright`.
