# ADR-0002: Modular monolith with four projects per module

Status: accepted
Date: 2026-09-19

## Context

The system must grow to hundreds of capabilities (finance, HR, timesheets, integrations, AI) for a small company with a two-person engineering capacity and a single on-premises server. Microservices would multiply deployment, observability and consistency work without a scaling need. A single unstructured project would not survive hundreds of features. The owner requires a deep domain → feature → sub-feature folder hierarchy and forbids dumping files into one folder.

## Decision

- One ASP.NET Core host process composes independent modules. A module is a bounded context inside a domain (`Finance/Sales`, `Finance/Payroll`, `Clients/Core`, `Platform/SystemInfo`).
- Every module has exactly four projects, co-located under `backend/Modules/<Domain>/<Module>/`: `Contracts` (the only assembly other modules may reference), `Module` (one implementation assembly, everything internal), `Tests/UnitTests` and `Tests/IntegrationTests`.
- Inside the module assembly, features are folders and layers are sub-folders: `<Feature>/{Domain, Application, Endpoints, Persistence, Reports, Ai}`. `Ai/` is always a sibling folder. A folder holds at most 12 source files; generated folders are exempt.
- Each module owns one DbContext, one database schema (`<domain>_<module>`), one route group (`/api/<domain>/<module>`), one feature flag (`Erp.Modules.<Domain>.<Module>`) and its permission constants. Cross-module reads go through `Contracts` query interfaces; cross-module writes go through integration events.
- `BuildingBlocks.*` projects hold technical concerns only and never reference a module. Host projects reference every module and contain only composition.
- Handlers are hand-rolled `ICommandHandler`/`IQueryHandler` implementations; EF Core is the persistence abstraction (no repository layer).
- Architecture tests and MSBuild targets enforce the boundaries; namespace equals folder is a build error.

## Consequences

- Adding a module is mechanical (four projects, one registration line, one frontend folder) and never touches another module.
- Project count grows by four per module; per-domain solution filters keep IDE loads manageable.
- A module that becomes too large is split into two modules with their own schemas; the promotion path is documented in `docs/architecture/adding-a-module.md`.
- Alternatives rejected: six-project layered modules (too much ceremony for a startup), one project per domain with a shared schema (no boundaries between features), microservices (operational cost without benefit).
