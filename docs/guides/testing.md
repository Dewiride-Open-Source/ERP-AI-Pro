# Testing

## Layers

| Layer | Where | Tooling | Runs against |
|---|---|---|---|
| Unit | `Modules/<D>/<M>/Tests/UnitTests`, `Tests/BuildingBlocks` | xUnit v3 on Microsoft.Testing.Platform, `FakeTimeProvider`, `FakeLogger<T>` | nothing external |
| Integration | `Modules/<D>/<M>/Tests/IntegrationTests`, `Tests/Host` | `WebApplicationFactory<Program>` from `Dewiride.Erp.Testing` (`ErpApiFactory`, `ErpApiFactory.ForEnvironment`) | nothing external today; a real SQL Server (`ERP_TEST_SQL_CONNECTION` locally, a service container in CI) and the test authentication handler arrive with the backend-platform and authentication phases |
| Architecture | `Tests/Architecture` | ArchUnitNET | compiled assemblies |
| Scripts | `scripts/**/tests` | `node --test` | files in a temp directory |
| End-to-end | `frontend/e2e` | Playwright | built web app + running API |

## Running

```bash
cd backend && dotnet test --solution Dewiride.Erp.slnx --report-trx --coverage --coverage-output-format cobertura
cd backend && dotnet test --project Modules/Platform/SystemInfo/Tests/UnitTests/Dewiride.Erp.Modules.Platform.SystemInfo.UnitTests
node --test "scripts/roadmap/tests/*.test.ts"
cd frontend && pnpm e2e                       # all projects
cd frontend && pnpm e2e -- --project=chromium  # one project
```

Results land in `backend/artifacts/TestResults/` (TRX + Cobertura) and `frontend/e2e/playwright-report/`.

## Rules

- Names: `Method_Condition_ExpectedResult`. One behaviour per test. No sleeps, no order dependence, no shared mutable state.
- Statutory logic: dated, table-driven `[Theory]` cases from the parameter sets, including the boundary dates on both sides of a change.
- Coverage: at least 80 % line coverage for Domain + Application per module; a pull request never lowers it.
- Every feature ships a Playwright spec that visits every page it adds and clicks every button, menu item, tab, dialog and form control (happy path plus one validation failure), asserts no console errors and takes an ARIA snapshot of key screens.
- Playwright runs every visual test in light and dark theme (`forEachTheme`) on desktop Chromium, Firefox, WebKit and the newest Pixel and iPhone descriptors; behavioural tests that render nothing theme-specific (redirects, header checks, the API smoke) run once, and `tests/smoke/**` runs on Chromium only. Pull requests run Chromium and one mobile project, `main` runs the full matrix. Screenshots are named `<spec>/<name>--<project>--<theme>.png`. A second web instance started without an API (`E2E_OFFLINE_BASE_URL`) covers the unavailable states.
- Tests obey the same comment and package rules as production code.
