# Dependency rules

Enforced by `backend/Tests/Architecture/Dewiride.Erp.ArchitectureTests`, `backend/build/BannedPackages.targets` and `scripts/checks/feature-boundaries.ts`.

## Backend assemblies

| From | May reference | Never references |
|---|---|---|
| `BuildingBlocks.Kernel` | BCL only | anything else |
| `*.Contracts` | `BuildingBlocks.Kernel`, `BuildingBlocks.SharedKernel` | framework packages, modules |
| `BuildingBlocks.<Concern>` | Kernel, framework packages, other BuildingBlocks | any module, any host |
| `Modules.<Domain>.<Module>` | own Contracts, BuildingBlocks.*, other modules' `*.Contracts` | another module's implementation, any host |
| `Hosts.*` | BuildingBlocks.*, every module | (contain only composition) |
| Test projects | the project under test, `Tests/Shared/Dewiride.Erp.Testing`, the API host (integration tests) | other modules' implementations |

## Namespaces inside a module

| Namespace segment | May reference | Never references |
|---|---|---|
| `<Feature>.Domain` | `BuildingBlocks.Kernel`, `SharedKernel` | EF Core, ASP.NET Core, DI, other modules |
| `<Feature>.Application` | Domain, the module DbContext, `BuildingBlocks.Application`, `Contracts` of other modules | ASP.NET Core types |
| `<Feature>.Endpoints` | Application, `BuildingBlocks.Endpoints` | EF Core, another feature's Application |
| `<Feature>.Persistence` | Domain, EF Core | ASP.NET Core |
| `<Feature>.Ai` | own Application/Domain, `BuildingBlocks.Ai` | provider SDKs |
| `Integration` | own Application, other modules' `Contracts` events | other modules' implementations |

## Other rules the tests assert

- Every module assembly contains exactly one public `IModule` implementation, and `Hosts/Api/Modules.cs` lists every one of them.
- Every type is `internal` unless it is a module class, lives in `Contracts`, or is a BuildingBlocks API.
- Every DbContext maps only entity types from its own assembly.
- Every endpoint group requires authorization unless the route is on the anonymous whitelist (`/healthz/*`, `/api/auth/login`, OIDC callbacks, `/api/platform/system-info` and `/api/platform/features` until authentication exists, OpenAPI in Development).
- No assembly references a banned package.
- Namespace equals folder for every type (also a compiler error through IDE0130).

## Frontend

- `src/app/**` imports only `@/features/<domain>/<module>` (the module `index.ts`) and `@/shared/**`.
- `src/features/<domain>/<module>/**` never imports another module's internals; cross-module UI goes through the other module's `index.ts`.
- `packages/ui` never imports from `apps/web`.
