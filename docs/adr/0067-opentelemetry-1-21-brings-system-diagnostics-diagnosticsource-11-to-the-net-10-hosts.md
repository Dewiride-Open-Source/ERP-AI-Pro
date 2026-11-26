# ADR-0067: OpenTelemetry .NET 1.21 brings System.Diagnostics.DiagnosticSource 11 to the .NET 10 hosts

Status: accepted
Date: 2026-11-26

## Context

- **The hosts track .NET 10 LTS.** `global.json`, the SDK and runtime images and every framework-versioned package (`Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore*`, `Microsoft.Extensions.*`, `System.Security.Cryptography.*`) stay on the 10 line, and `.github/dependabot.yml` ignores their majors: such a package moves a major only with an ADR.
- **OpenTelemetry .NET 1.21.0** (core-1.21.0 of 2026-11-11, with OpenTelemetry.Instrumentation.AspNetCore, .Http and .SqlClient 1.21.0) makes `OpenTelemetry.Api` depend on `System.Diagnostics.DiagnosticSource` 11.0.0 for every target, `net10.0` included (open-telemetry/opentelemetry-dotnet#8066). The package is transitive: nothing in `Directory.Packages.props` names it, and it enters the lock files of the API, composition and migrator hosts and the API image.
- **The package supports .NET 10.** `System.Diagnostics.DiagnosticSource` ships outside the shared framework as well, and its 11.0.0 package targets `net462`, `netstandard2.0`, `net10.0` and `net11.0`. An application that carries it loads its own `System.Diagnostics.DiagnosticSource.dll` 11.0.0.0 instead of the shared framework's 10.0.0.0, because the runtime takes the higher assembly version of an application-local copy.
- **The alternative** was to stay on OpenTelemetry core 1.20.1 with the instrumentation packages 1.20.2, which carry the same fixes (GHSA-6xq4-gr25-qg2x, high, in OpenTelemetry.Instrumentation.AspNetCore among them) without the .NET 11 package. Later OpenTelemetry releases follow the newest `System.Diagnostics.DiagnosticSource`, so the telemetry packages would stay behind until the hosts move to the next LTS.
- **Evidence.** With 1.21.0 every one of the 7,199 .NET tests passes, `TelemetryTests` and `DatabaseTelemetryTests` among them, and the release notes of core 1.21.0 and of the three instrumentation packages change no option or default the hosts rely on (the SqlClient instrumentation keeps `RecordException` off, ASP.NET Core traces still leave out `/healthz/*` and redact query values).

## Decision

- **Adopt OpenTelemetry .NET 1.21.0 with `System.Diagnostics.DiagnosticSource` 11.0.0 on the .NET 10 hosts** (owner decision of 2026-11-26).
- **The package stays transitive.** It moves with `OpenTelemetry.Api`; every framework-versioned package the solution names, the SDK and the images stay on the 10 line, and the Dependabot ignore rules for their majors are unchanged.
- **A security release of the 11.0 line is pinned.** When an 11.0.x release of `System.Diagnostics.DiagnosticSource` carries a security fix before OpenTelemetry depends on it, `Directory.Packages.props` gains a `PackageVersion` for it, which `CentralPackageTransitivePinningEnabled` applies to the transitive reference, as it does for `Microsoft.Identity.Client` and `Microsoft.IdentityModel.*`.

## Consequences

- **Two release lines to read.** Each dependency round reads the .NET 11 release notes (`release-notes/11.0/releases.json` of dotnet/core) besides the 10.0 ones, for security releases of `System.Diagnostics.DiagnosticSource`.
- **The API image carries the package.** Its application folder holds `System.Diagnostics.DiagnosticSource.dll` 11.0.0, which the image's notices list; the runtime's own copy of the assembly is not used.
- **Activities and meters come from the 11.0 implementation** in every host, the framework's own instrumentation (ASP.NET Core, HttpClient, SqlClient) included, because there is one copy of the assembly per process.
