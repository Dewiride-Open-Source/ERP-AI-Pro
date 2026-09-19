# ADR-0009: Third-party package approvals and bans

Status: accepted
Date: 2026-09-19

## Context

The owner requires Microsoft-first choices, production-grade quality and no vulnerable or deprecated packages, and wants to approve every non-Microsoft dependency. Several widely used .NET libraries moved to commercial licences in 2025 (MediatR 13+, AutoMapper 15+, FluentAssertions 8+).

## Decision

Pre-approved publishers: Microsoft, Azure SDK, .NET Foundation `System.*`, OpenTelemetry (CNCF, the path Microsoft documents), Vercel (`next`, `@vercel/otel`), Meta (`react`), OpenJS (`eslint`, `prettier`), Tailwind Labs.

Approved by the owner on 2026-09-19 (licence verified at first use):

| Package | Ecosystem | Purpose | Microsoft alternative considered |
|---|---|---|---|
| shadcn/ui (`shadcn` CLI), `radix-ui` | npm | accessible primitives and component recipes | none |
| `motion` | npm | orchestrated animations (enters with the web-foundation phase) | CSS/Tailwind animations cover simple cases |
| `geist` | npm | Geist Sans and Mono font files (Vercel) | none |
| `typescript-eslint` (through `eslint-config-next/typescript`) | npm | type-aware lint rules such as `no-floating-promises` for Playwright specs | none |
| `next-themes` | npm | class-based theme with system preference | cookie-based theme (more code, no system detection) |
| `lucide-react`, `class-variance-authority`, `clsx`, `tailwind-merge`, `tw-animate-css`, `sonner` | npm | shadcn runtime dependencies | none |
| `@tanstack/react-query`, `@tanstack/react-table` | npm | client data caching and tables (web foundation phase) | SWR (Vercel) evaluated in that phase |
| `react-hook-form`, `zod` | npm | forms and schema validation | none |
| `TngTech.ArchUnitNET` | NuGet | architecture tests | reflection-based tests (more code) |
| `Scalar.AspNetCore` | NuGet | OpenAPI reference UI in Development | none in ASP.NET Core 10 |
| `xunit.v3` (`xunit.v3.mtp-v2`) | NuGet | test framework with MTP support | MSTest (viable; xUnit chosen for ecosystem familiarity) |
| `Shouldly` | NuGet | optional readable assertions | xUnit `Assert` is the default |
| `Testcontainers` | NuGet | only if CI needs more than a service container | GitHub service container |
| `FluentValidation` | NuGet | only if built-in validation proves insufficient, via a new ADR | ASP.NET Core 10 built-in validation |

Banned: MediatR, AutoMapper, FluentAssertions (all versions — commercial licences); Newtonsoft.Json, Serilog, NLog, Swashbuckle, Moq, Semantic Kernel (Microsoft or built-in alternatives exist). `backend/build/BannedPackages.targets` fails the build on the NuGet bans.

Any other package requires the owner's approval with name, licence, maintainer and the reason no Microsoft or built-in option fits.

## Consequences

- Mapping is explicit code; request/command dispatch is hand-rolled handlers; assertions are xUnit `Assert`.
- Every approval is traceable to this ADR; later approvals are appended in a superseding ADR.
