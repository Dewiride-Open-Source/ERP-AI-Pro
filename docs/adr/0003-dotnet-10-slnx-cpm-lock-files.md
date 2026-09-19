# ADR-0003: .NET 10 LTS, XML solution format, central package management and host lock files

Status: accepted
Date: 2026-09-19

## Context

Microsoft Learn lists .NET 10 as the current long-term-support release (supported until November 2028) and .NET 11 as a short-term-support release candidate. The .NET 10 SDK creates `.slnx` solutions by default, audits transitive NuGet packages by default, and ships central package management and lock-file support. The owner requires the latest non-vulnerable packages and a build that cannot silently drift.

## Decision

- Target `net10.0` everywhere; never adopt a short-term-support or preview runtime for production code. `global.json` pins the SDK feature band with `rollForward: latestFeature` and selects Microsoft.Testing.Platform.
- The solution is `backend/Dewiride.Erp.slnx`; solution folders mirror the directory tree.
- Package versions live only in `backend/Directory.Packages.props` (`ManagePackageVersionsCentrally`, `CentralPackageTransitivePinningEnabled`, `CentralPackageVersionOverrideEnabled=false`).
- NuGet audit runs in mode `all` at level `low`; moderate, high and critical advisories (NU1902–NU1904) are build errors, low advisories (NU1901) are warnings; suppressions require an ADR.
- Deployable hosts (`Hosts/Api`, `Hosts/HealthProbe`, later `Hosts/Migrator`) commit `packages.lock.json` and restore with `--locked-mode` in CI; libraries and modules do not keep lock files, following NuGet guidance that lock files belong at the top of the dependency chain.
- `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`, `Nullable=enable`, `ImplicitUsings=enable`, `UseArtifactsOutput=true` and `InvariantGlobalization=false` (INR and IST need ICU) are repository-wide defaults; `LangVersion` is never set.
- `Microsoft.CodeAnalysis.BannedApiAnalyzers` bans `DateTime.Now`/`UtcNow`, `DateTimeOffset.Now`, `Guid.NewGuid()`, `Thread.Sleep`, `Task.Result` and `.Wait()` in favour of `TimeProvider`, `Guid.CreateVersion7()` and async code.

## Consequences

- Dependabot bumps one file for NuGet; every project inherits the same version.
- The .NET 12 LTS (November 2027) is the next planned runtime move.
- Low-severity advisories in transitive packages produce warnings that Dependabot resolves; they never block a build.
