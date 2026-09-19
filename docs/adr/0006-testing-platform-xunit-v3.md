# ADR-0006: Microsoft.Testing.Platform with xUnit v3

Status: accepted
Date: 2026-09-19

## Context

.NET 10 ships two test platforms. Microsoft documents that VSTest-based and Microsoft.Testing.Platform (MTP) based projects must not be mixed in one solution. `dotnet test` switches to MTP mode through `global.json`, test projects become executables, and the `dotnet new xunit` template on the .NET 10 SDK still scaffolds xUnit v2 with VSTest, so MTP projects are authored by hand. FluentAssertions moved to a commercial licence.

## Decision

- `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`. Every test project sets `OutputType=Exe` and `UseMicrosoftTestingPlatformRunner=true` through `backend/build/Tests.props`, references `xunit.v3.mtp-v2` (the xUnit v3 package built for Microsoft.Testing.Platform v2), `Microsoft.Testing.Extensions.TrxReport` and `Microsoft.Testing.Extensions.CodeCoverage`, and shares `backend/testconfig.json`.
- VSTest packages (`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.*`) never enter the solution.
- Assertions use xUnit `Assert`; Shouldly is approved but optional.
- Test doubles: `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) and `FakeLogger<T>` (`Microsoft.Extensions.Diagnostics.Testing`); interface mocks only when a fake is not available.
- Architecture rules are written with ArchUnitNET.
- Integration tests use `WebApplicationFactory<Program>` from `Tests/Shared/Dewiride.Erp.Testing`; every integration-test project references the API host directly so the content root resolves (the `.sln`-search fallback does not work with `.slnx`).
- Database-backed integration tests (from the backend-platform phase) run against a real SQL Server: the owner's instance locally (`ERP_TEST_SQL_CONNECTION`), a SQL Server 2025 service container in CI; a fresh database per test run. The InMemory and SQLite providers are never used.

## Consequences

- `dotnet test --solution Dewiride.Erp.slnx --report-trx --coverage --coverage-output-format cobertura` runs every suite with one command.
- Test projects are hand-authored from the shared props; no template is needed.
- The database reset strategy between tests (per-run database vs table truncation) is decided in the backend platform phase.
