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

`Hosts.Composition` holds `Modules.All` and `AddErpPlatform`, shared by the API host, the test database host and the migrator.

`BuildingBlocks.Idempotency` references `BuildingBlocks.Application` and `BuildingBlocks.Persistence`: it owns the schema `platform_idempotency` and its middleware. `BuildingBlocks.Attachments` references `BuildingBlocks.Application`, `BuildingBlocks.Kernel`, `BuildingBlocks.Persistence` and the package `Azure.Storage.Blobs`: it owns the schema `files`, the storage and `IAttachmentService`. `Hosts.Composition` references it to call `AddErpAttachments()`; the module `Platform/Attachments` references it to expose the routes, and a business module that attaches files references it too, never the `Platform/Attachments` module ([attachments](attachments.md)). `BuildingBlocks.Endpoints` references `BuildingBlocks.Application` (for `IActorContext`, which `HttpActorContext` implements) and `BuildingBlocks.Persistence` references `BuildingBlocks.Application` (for `IActorContext` and `SystemActorContext`, consumed by `AuditingSaveChangesInterceptor` and registered by `AddErpPersistenceCore`).

## Namespaces inside a module

| Namespace segment | May reference | Never references |
|---|---|---|
| `<Feature>.Domain` | `BuildingBlocks.Kernel`, `SharedKernel` | EF Core, ASP.NET Core, DI, other modules |
| `<Feature>.Application` | Domain, the module DbContext, `BuildingBlocks.Application`, `Contracts` of other modules | ASP.NET Core types |
| `<Feature>.Endpoints` | Application, `BuildingBlocks.Endpoints` | EF Core, another feature's Application |
| `<Feature>.Persistence` | Domain, EF Core | ASP.NET Core |
| `<Feature>.Hosting` | Application, Domain, `BuildingBlocks.*`, `Microsoft.Extensions.*` (hosting, DI, logging, feature management) | ASP.NET Core, EF Core, Endpoints |
| `<Feature>.Ai` | own Application/Domain, `BuildingBlocks.Ai` | provider SDKs |
| `Integration` | own Application, other modules' `Contracts` events | other modules' implementations |

## Layer rules (`LayerTests`)

`Tests/Architecture/Dewiride.Erp.ArchitectureTests/Rules/LayerTests.cs` evaluates these rules over the architecture ArchUnitNET builds from every `Dewiride.Erp.*` assembly in the test output, test assemblies and `Dewiride.Erp.Testing` excluded (`ErpAssemblies`):

| Test | Rule |
|---|---|
| `Domain_DependsOnlyOnKernelSharedKernelAndSystem` | types in `Dewiride.Erp.Modules.*.Domain` depend only on `System.*`, `BuildingBlocks.Kernel`, `BuildingBlocks.SharedKernel` and module `Domain` namespaces |
| `Application_DoesNotDependOnAspNetCore` | module `Application` types never use `Microsoft.AspNetCore.*` |
| `Endpoints_DoNotDependOnEntityFrameworkOrPersistence` | module `Endpoints` types never use `Microsoft.EntityFrameworkCore.*` or a module `Persistence` namespace |
| `Endpoints_DoNotDependOnDomain` | module `Endpoints` types never use a module `Domain` namespace |
| `Application_DoesNotDependOnEndpoints` | module `Application` types never use a module `Endpoints` namespace |
| `Persistence_DoesNotDependOnAspNetCoreOrEndpoints` | module `Persistence` types never use ASP.NET Core or a module `Endpoints` namespace |
| `Hosting_DoesNotDependOnAspNetCoreEntityFrameworkOrEndpoints` | module `Hosting` types never use ASP.NET Core, EF Core or a module `Endpoints` namespace |
| `AzureStorage_IsReferencedOnlyByTheBlobStorageNamespace` | no type outside `Dewiride.Erp.BuildingBlocks.Attachments.Storage.Blob` depends on an `Azure.Storage.*` type, so the storage client library stays behind `IDocumentStore` and a module or another building block can never reach Blob Storage directly; the test first asserts that the `Azure.Storage` provider is not empty, so the rule cannot pass vacuously |
| `Handlers_AreSealed` | every command and query handler class is sealed |

The object providers for framework namespaces (`Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `Azure.Storage`) and the Domain rule's allowed set are built with `Types(true)`. `Types()` holds only the types defined in the loaded `Dewiride.Erp.*` assemblies, so a provider over a framework namespace built with it is empty and a `NotDependOnAny` rule over it can never fail; `Types(true)` also includes the types those assemblies reference, so the rules test the real framework dependencies. The subjects of each rule (module `Domain`, `Application`, `Endpoints`, `Persistence`, `Hosting`, and the blob storage namespace) stay `Types()`, because they are ERP types. The module rules match `Dewiride.Erp.Modules.*` namespaces only; a building block's own `Domain` folder (`BuildingBlocks.Attachments.Domain`) is held to the same shape by review, not by these rules.

## Other rules the tests assert

- Every module assembly contains exactly one public `IModule` implementation, and `Modules.All` in `Hosts/Composition/Dewiride.Erp.Host.Composition` lists every one of them.
- Every exported type in a module assembly is the module class, an EF Core migration or model snapshot (scaffolded public), or a request record under `<Feature>.Endpoints.Requests` (public because the built-in validation source generator skips internal types). Everything else is `internal`.
- Every module descriptor follows the naming table in `naming-and-namespaces.md`: assembly `Dewiride.Erp.Modules.<Domain>.<Module>`, schema `<domain>_<module>` when present, route prefix `/<domain>/<module>`, flag `Erp.Modules.<Domain>.<Module>`, permissions `<domain>.<module>.<feature>.<action>` starting with the module's own prefix, PascalCase capability names. `ModuleCatalog` re-checks at startup that no two modules share an id, route prefix, schema or flag and that every permission carries its module prefix.
- Every module lives at `Modules/<Domain>/<Module>/` with `README.md` and the four projects `Contracts/`, `Module/`, `Tests/UnitTests/`, `Tests/IntegrationTests/` named after the assembly.
- Every endpoint group requires authorization unless the route is on the anonymous whitelist (`/healthz/*`, `/api/auth/login`, OIDC callbacks, OpenAPI in Development, and until authentication exists `/api/platform/system-info`, `/api/platform/system-info/startups`, `/api/platform/features` and the attachment routes `/api/platform/attachments`, `/api/platform/attachments/policy`, `/api/platform/attachments/{id:guid}`, `/api/platform/attachments/{id:guid}/download-links` and `/api/platform/attachments/{id:guid}/content`; `ModuleCatalogTests`).
- Every type under `<Feature>.Endpoints.Requests` is a public sealed record whose attributes all use the `property:` target, and every such record that carries validation rules is known to the running `ValidationOptions`, which fails when the module never calls `AddValidation()` (`RequestRecordTests`).
- No assembly references a banned package.
- Namespace equals folder for every `.cs` file under `BuildingBlocks/`, `Hosts/`, `Modules/` and `Tests/` (`Migrations/` folders exempt; also a compiler error through IDE0130), and no folder holds more than 12 source files.

`PersistenceTests` assert, against the contexts registered in the `DbContextCatalog` of a test database host (see `persistence.md`):

- Every DbContext in a module assembly derives from `ModuleDbContext` and lives in the namespace `<Module>.Persistence`.
- Every catalogue context maps only entity types from its own assembly.
- A module context's schema equals its descriptor's `Schema`. Schemas owned by building blocks (`platform_idempotency`, `files`) are named by concern and sit outside the `<domain>_<module>` descriptor rule; the module that exposes a building block's routes declares `Schema: null` (`Platform/Attachments`).
- Every strongly-typed id property in every catalogue model uses `StronglyTypedIdConverter`.

## Frontend

- `src/app/**` imports only `@/features/<domain>/<module>` (the module `index.ts`) and `@/shared/**`.
- `src/features/<domain>/<module>/**` never imports another module's internals; cross-module UI goes through the other module's `index.ts`.
- `packages/ui` never imports from `apps/web`.
- `packages/api-client/src/generated` is written only by `scripts/api-client/generate.ts`; `scripts/api-client/drift.ts` fails when it differs from what `docs/openapi/erp.json` generates.
