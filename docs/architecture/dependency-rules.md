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
- Every exported type in a module assembly is the module class, an EF Core migration or model snapshot (scaffolded public), or a request record under `<Feature>.Endpoints.Requests` (public because the built-in validation source generator skips internal types). Everything else is `internal`.
- Every module descriptor follows the naming table in `naming-and-namespaces.md`: assembly `Dewiride.Erp.Modules.<Domain>.<Module>`, schema `<domain>_<module>` when present, route prefix `/<domain>/<module>`, flag `Erp.Modules.<Domain>.<Module>`, permissions `<domain>.<module>.<feature>.<action>` starting with the module's own prefix, PascalCase capability names. `ModuleCatalog` re-checks at startup that no two modules share an id, route prefix, schema or flag and that every permission carries its module prefix.
- Every module lives at `Modules/<Domain>/<Module>/` with `README.md` and the four projects `Contracts/`, `Module/`, `Tests/UnitTests/`, `Tests/IntegrationTests/` named after the assembly.
- Every endpoint group requires authorization unless the route is on the anonymous whitelist (`/healthz/*`, `/api/auth/login`, OIDC callbacks, `/api/platform/system-info` and `/api/platform/features` until authentication exists, OpenAPI in Development).
- No assembly references a banned package.
- Namespace equals folder for every `.cs` file under `BuildingBlocks/`, `Hosts/`, `Modules/` and `Tests/` (`Migrations/` folders exempt; also a compiler error through IDE0130), and no folder holds more than 12 source files.

The persistence sub-phase of the backend platform adds the rule that every DbContext maps only entity types from its own assembly.

## Frontend

- `src/app/**` imports only `@/features/<domain>/<module>` (the module `index.ts`) and `@/shared/**`.
- `src/features/<domain>/<module>/**` never imports another module's internals; cross-module UI goes through the other module's `index.ts`.
- `packages/ui` never imports from `apps/web`.
