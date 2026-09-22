# Testing

## Layers

| Layer | Where | Tooling | Runs against |
|---|---|---|---|
| Unit | `Modules/<D>/<M>/Tests/UnitTests`, `Tests/BuildingBlocks` | xUnit v3 on Microsoft.Testing.Platform, `FakeTimeProvider`, `FakeLogger<T>` | nothing external |
| Integration | `Modules/<D>/<M>/Tests/IntegrationTests`, `Tests/Host`, `Tests/BuildingBlocks` (SQL-backed projects) | `WebApplicationFactory<Program>` from `Dewiride.Erp.Testing` (`ErpApiFactory`, `ErpApiFactory.ForEnvironment`); the `SqlTestDatabase` assembly fixture | a real SQL Server and no Azure: the `SqlTestDatabase` assembly fixture creates `ErpAiProTest_<yyyyMMddHHmmss>_<8 hex>` per test process from `ERP_TEST_SQL_CONNECTION` (the owner's instance locally, the `start-sql-server` action in CI), migrates every catalogue context and drops the database at the end; a missing variable fails with a message naming it. Every `ErpApiFactory`, Development or Production, forces the in-memory configuration source (`ERP_CONFIGURATION_SOURCE=InMemory`, `APPCONFIG_ENDPOINT` blanked) so tests never need an endpoint and a developer's user secrets cannot connect a test host to the store; the test authentication handler arrives with the authentication phase |
| Architecture | `Tests/Architecture` | ArchUnitNET; one `ErpApiFactory` host for `ModuleCatalogTests` and `PersistenceTests` | compiled assemblies plus the `SqlTestDatabase` per-process database (the project declares the assembly fixture, so it needs `ERP_TEST_SQL_CONNECTION` like the integration projects); its `coverage.settings.xml` excludes every module from coverage instrumentation, because ArchUnitNET reads the assemblies from disk and the static instrumentation the coverage tool uses on Linux rewrites them for the duration of the run |
| Web unit | `frontend/apps/web/src/**/*.test.ts`, next to the code | `node --test` on Node 24 (native type stripping, no extra packages); `pnpm test:unit`; `Method_Condition_ExpectedResult` titles | pure modules only (the environment schema); nothing external |
| Scripts | `scripts/roadmap/tests`, `scripts/checks/tests` | `node --test`; sentence-style titles (`test('a labelled override needs an unlabelled default', ...)`) | the roadmap model in a temp directory; the seed files under `infra/appconfig`; a throwaway git repository for the secret-pattern check |
| End-to-end | `frontend/e2e` | Playwright | built web app + running API |

## Running

```bash
cd backend && dotnet test --solution Dewiride.Erp.slnx --report-trx --coverage --coverage-output-format cobertura
cd backend && dotnet test --project Modules/Platform/SystemInfo/Tests/UnitTests/Dewiride.Erp.Modules.Platform.SystemInfo.UnitTests
node --test "scripts/roadmap/tests/*.test.ts"
node --test "scripts/checks/tests/*.test.ts"     # seed-file conventions, the secret-pattern check and every other repository check
cd frontend && pnpm test:unit                    # web unit tests (src/**/*.test.ts)
cd frontend && pnpm e2e                          # all projects
cd frontend && pnpm e2e --project=chromium       # one project
```

Results land in `backend/artifacts/TestResults/` (TRX + Cobertura) and `frontend/e2e/playwright-report/`.

## Rules

- Names: `Method_Condition_ExpectedResult` for .NET tests and for web unit tests (`src/**/*.test.ts`); sentence-style titles for `node --test` files under `scripts/**/tests`. One behaviour per test. No sleeps, no order dependence, no shared mutable state.
- Database under test: a SQL-backed test project declares `[assembly: AssemblyFixture(typeof(SqlTestDatabase))]` once, which creates the per-process database before the first test and drops it after the last; `ErpApiFactory` injects `SqlTestDatabase.Current.ConnectionString` as `Erp:Platform:Database:ConnectionString` into every test host and throws a message naming that `AssemblyFixture` line when no test database exists, so a test host can never reach the developer's own `ErpAiPro` database; `WithConfiguration("Erp:Platform:Database:ConnectionString", ...)` overrides the injection for a test that needs its own database; a test that needs a context outside the host (a migration test, a convention test) takes it from a class fixture built on `SqlTestDatabase.Current`. Every test host reports the readiness checks `self` and `database:platform_system_info`.
- Configuration under test: `ErpApiFactory.WithConfiguration(key, value)` passes the value to the test host as a command-line setting, the highest-priority source in a test host, so it overrides `appsettings.json` and also the values the host reads while it is being built (such as `Erp:Platform:Configuration:*`); call it before the first client or service is requested (it throws afterwards). The factory's own `APPCONFIG_ENDPOINT` and `ERP_CONFIGURATION_SOURCE` settings are applied last, so a test can never re-point the host at the store. `ErpApiFactory.WithFeature(name, enabled)` writes a flag in the Microsoft schema the same way, so a test proves both the enabled and the disabled state of a module without Azure (a later `WithFeature` for the same name wins, mirroring provider order).
- Disabled-module states in Playwright come from a second API and web pair (`E2E_GATED_API_BASE_URL`, `E2E_GATED_BASE_URL`; ports 5081 and 3002 when Playwright starts them) whose API runs with `feature_management__feature_flags__0__id=Erp.Modules.Platform.SystemInfo` and `__enabled=false`; specs that need it `test.skip` when the pair is absent and `test.use({ baseURL })` to target it.
- Statutory logic: dated, table-driven `[Theory]` cases from the parameter sets, including the boundary dates on both sides of a change.
- Coverage: at least 80 % line coverage for Domain + Application per module; a pull request never lowers it.
- Every feature ships a Playwright spec that visits every page it adds and clicks every button, menu item, tab, dialog and form control (happy path plus one validation failure), asserts no console errors and takes an ARIA snapshot of key screens.
- Playwright runs every visual test in light and dark theme (`forEachTheme`) on desktop Chromium, Firefox, WebKit and the newest Pixel and iPhone descriptors; behavioural tests that render nothing theme-specific (redirects, header checks, the API smoke) run once, and `tests/smoke/**` runs on Chromium only. Pull requests run Chromium and one mobile project, `main` runs the full matrix. Screenshots are named `<spec>/<name>--<project>--<theme>.png`. A second web instance started without an API (`E2E_OFFLINE_BASE_URL`) covers the unavailable states.
- Tests obey the same comment and package rules as production code.
