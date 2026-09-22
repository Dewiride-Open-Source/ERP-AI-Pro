# Persistence

EF Core 10 on SQL Server: the owner's SQL Server 2025 Developer instance for development, Azure SQL in production, one database, one schema per module, migrations owned by the module and applied outside the running process (ADR-0008, ADR-0013). `Dewiride.Erp.BuildingBlocks.Persistence` holds the base context, the registration extension, the conventions, the context catalogue and the migrator; a module never wires the provider itself.

## Provider and options

`DatabaseOptions` binds the section `Erp:Platform:Database` with `ValidateDataAnnotations().ValidateOnStart()`:

| Key | Type | Default | Range | Purpose |
|---|---|---|---|---|
| `Erp:Platform:Database:ConnectionString` | string | — | required at run time, may be absent at EF design time | connection string of the one ERP database |
| `Erp:Platform:Database:Provider` | `DatabaseProvider` | `SqlServer` | `SqlServer` or `AzureSql` | selects `UseSqlServer` or `UseAzureSql` |
| `Erp:Platform:Database:CommandTimeout` | TimeSpan | `00:00:30` | 1 second to 10 minutes | command timeout on every module context |
| `Erp:Platform:Database:MaxRetryCount` | int | `5` | 0 to 20 | `EnableRetryOnFailure` retry count |
| `Erp:Platform:Database:MaxRetryDelay` | TimeSpan | `00:00:10` | 1 second to 2 minutes | `EnableRetryOnFailure` maximum delay between retries |

The connection string reaches the process differently in each situation; nothing in the repository holds one:

| Process | Where the connection string comes from |
|---|---|
| `dotnet run` with the store (`APPCONFIG_ENDPOINT` set) | the Key Vault reference `Erp:Platform:Database:ConnectionString` → secret `Erp--Platform--Database--ConnectionString` in the environment's vault (labelled `local-dev`; the `production` reference is added by the first-deployment phase) |
| `dotnet run` without the store, and every `dotnet ef` command | `dotnet user-secrets` of `Hosts/Api/Dewiride.Erp.Host.Api` (key `Erp:Platform:Database:ConnectionString`) on a developer machine, the environment variable `Erp__Platform__Database__ConnectionString` in CI |
| tests | `ERP_TEST_SQL_CONNECTION` through `SqlTestDatabase`, which injects the per-process test database into every `ErpApiFactory` (section "Test databases") |
| containers | the key-per-file secret `/run/secrets/Erp__Platform__Database__ConnectionString`, mounted by `compose.override.yaml` from the git-ignored file `infra/compose/secrets/Erp__Platform__Database__ConnectionString` |
| CI | the outputs of `.github/actions/start-sql-server` (section "CI") |

## `ModuleDbContext` and `AddModuleDbContext<TContext>(schema)`

Every module DbContext derives from `ModuleDbContext`, lives in `<Module>.Persistence` and is registered from the module's `AddServices` with `builder.AddModuleDbContext<TContext>(schema)`, which configures:

- `HasDefaultSchema(schema)` and the migrations history table `__EFMigrationsHistory` inside that schema;
- the provider from `Erp:Platform:Database:Provider`: `UseSqlServer(connectionString, o => o.UseCompatibilityLevel(170).EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay, null).CommandTimeout(CommandTimeout))` for `SqlServer`, `UseAzureSql(...)` with the same history table, timeout and retry settings for `AzureSql`;
- a readiness health check named `database:<schema>` (`AddDbContextCheck<TContext>`) tagged `ready`, reported by `/healthz/ready`;
- an entry in the `DbContextCatalog`, which `DatabaseMigrator.MigrateAllAsync` walks in registration order.

`ErpHostComposition.AddErpPlatform(this IHostApplicationBuilder builder, Assembly hostAssembly)` in `Hosts/Composition/Dewiride.Erp.Host.Composition` runs the design-time source rule, then `AddErpConfiguration(hostAssembly)`, `AddErpPersistence()`, `AddErpIdempotency()` and `AddModules(Modules.All)`. The API host, the test database host and the migrator all call it, so no context can be missing from the catalogue.

## Conventions

- Strongly-typed ids are `readonly record struct`s implementing `IStronglyTypedId<TSelf>` (`Guid Value`; `Create()` returns `Guid.CreateVersion7()`; `From(Guid)`). `StronglyTypedIdConverter<TId>` maps each one to `uniqueidentifier`; the convention discovers every id type in the context assembly and in the `Dewiride.Erp.*` assemblies it references, so no entity configuration names the converter.
- `decimal` properties default to precision 19, scale 4.
- `DateTimeOffset` values are normalised to UTC on write and on read by `UtcDateTimeOffsetConverter`.
- Value objects are readonly record structs mapped as complex types through `ComplexProperty` in the entity configuration (the `[ComplexType]` attribute is class-only, so the Domain stays attribute-free), by table splitting with column names `<Property>_<Member>` (`Build_Version`, `Build_Framework`).
- `Money` (`Dewiride.Erp.BuildingBlocks.Kernel.Monetary`) is a complex type by convention: `ConfigureConventions` declares `ComplexProperties<Money>()` and `Properties<Currency>().HaveConversion<CurrencyConverter>().HaveMaxLength(3).AreUnicode(false).AreFixedLength()`, so every `Money` property maps to `<Property>_Amount decimal(19,4)` and `<Property>_Currency char(3)` (`Price_Amount`, `Price_Currency`) with no entity configuration. `CurrencyConverter` stores `Currency.Code` and reads back through `Currency.FromCode`, so a code outside the fixed table fails on read.
- `IDomainEvent` collections on aggregates are ignored by the model.
- `OnModelCreating` applies `ApplyConfigurationsFromAssembly(GetType().Assembly)` and then `ModelRules.ApplySoftDelete().ApplyRowVersion()` (next section).

## Auditing, soft delete and row versions

Three getter-only interfaces in `Dewiride.Erp.BuildingBlocks.Kernel.Domain` opt an entity in; implementers declare the properties with `private set` and never assign them, because `AuditingSaveChangesInterceptor` writes them through the change tracker:

| Interface | Members | Applied by |
|---|---|---|
| `IAuditable` | `DateTimeOffset CreatedAt`, `Guid CreatedBy`, `DateTimeOffset? ModifiedAt`, `Guid? ModifiedBy` | the interceptor |
| `ISoftDeletable` | `bool IsDeleted`, `DateTimeOffset? DeletedAt`, `Guid? DeletedBy` | the interceptor and the `SoftDelete` query filter |
| `IVersioned` | `byte[] RowVersion` | `ModelRules.ApplyRowVersion` (`IsRowVersion()`) |

`AuditingSaveChangesInterceptor(IActorContext actor, TimeProvider timeProvider)` in `BuildingBlocks.Persistence/Auditing` is a scoped `SaveChangesInterceptor` that `AddErpPersistenceCore` registers with `TryAddScoped` and `AddModuleDbContext` adds to every module context with `AddInterceptors`. Its synchronous `SavingChanges` throws `NotSupportedException`: every database write is `SaveChangesAsync`. `SavingChangesAsync` calls the static `Stamp(ChangeTracker, DateTimeOffset now, Guid actorId)` with `timeProvider.GetUtcNow()` and `actor.ActorId`:

| Entry state | Entity | Stamps |
|---|---|---|
| `Added` | `IAuditable` | `CreatedAt`/`CreatedBy` = now/actor; `ModifiedAt`/`ModifiedBy` = `null` |
| `Added` | `ISoftDeletable` | `IsDeleted` = `false`; `DeletedAt`/`DeletedBy` = `null` |
| `Modified` | `IAuditable` | `ModifiedAt`/`ModifiedBy` = now/actor; `CreatedAt`/`CreatedBy` marked unmodified so the row keeps its original values |
| `Modified` | `ISoftDeletable` | by the `IsDeleted` transition: `false` → `true` sets `DeletedAt`/`DeletedBy` = now/actor; `true` → `false` clears them; no transition marks `IsDeleted`, `DeletedAt`, `DeletedBy` unmodified (a tampered value or `Update(detached)` cannot resurrect a row or rewrite its deleter) |
| `Deleted` | `ISoftDeletable`, original `IsDeleted` = `true` | state becomes `Unchanged`; the first deletion's stamps stay |
| `Deleted` | `ISoftDeletable` | state becomes `Modified`; `IsDeleted` = `true`; `DeletedAt`/`DeletedBy` = now/actor; when the entity is also `IAuditable`, `ModifiedAt`/`ModifiedBy` = now/actor and `CreatedAt`/`CreatedBy` marked unmodified |
| `Deleted` | not `ISoftDeletable` | the row is deleted |

- `ModelRules.ApplySoftDelete` adds the named query filter `SoftDeleteFilter.Name` (`"SoftDelete"`, `entity => !entity.IsDeleted`) to every entity type that introduces `ISoftDeletable` into its hierarchy, so every query hides deleted rows; derived types inherit the root's filter. A derived type implementing it while its root does not, or an owned type implementing it, fails model building with an `InvalidOperationException` naming the type (EF Core allows a filter only on the root). An administrative read calls `SoftDeleteFilter.IncludeDeleted<TEntity>()`, which is `IgnoreQueryFilters([SoftDeleteFilter.Name])` and leaves every other named filter in force.
- `ModelRules.ApplyRowVersion` maps `IVersioned.RowVersion` with `IsRowVersion()` on the type that introduces the interface: a SQL Server `rowversion` column, a concurrency token generated on add and update, so a stale update surfaces as `DbUpdateConcurrencyException` from `SaveChangesAsync`; no entity configuration or interceptor catches it, and `EfUnitOfWork` — the unit of work of the application pipeline — turns it into `Error.Conflict("concurrency.conflict", …)` naming the entity types (`application-pipeline.md`).
- `ModuleDbContext` sets `ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges`: EF cascades only entries still `Deleted` after the interceptor ran, so `Remove(root)` on a soft-deletable aggregate leaves its loaded child entities and owned types untouched (the child rows stay, reachable through the hidden root), while a hard-deleted root still cascades at save time.
- `ExecuteUpdateAsync` and `ExecuteDeleteAsync` run without the change tracker, so `BulkWriteGuardInterceptor` (an `IQueryExpressionInterceptor` on every module context) throws `InvalidOperationException` at query compilation for `ExecuteDelete` on an `ISoftDeletable` type and for `ExecuteUpdate` on an `IAuditable` or `ISoftDeletable` type; every other entity type keeps both operations, and the `SoftDelete` filter still applies to their source query.

The BuildingBlocks integration tests prove these conventions against SQL Server on `SampleAggregate` (`IAuditable`, `ISoftDeletable`, `IVersioned`, a `Money` price) under `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.IntegrationTests/Persistence/`: `AuditingTests`, `SoftDeleteTests` (including a soft-deleted root that keeps its `SampleLine` child rows), `ConcurrencyTests`, `MoneyMappingTests` and `BulkWriteGuardTests`, with `FakeTimeProvider` as the clock and `TestActorContext` as the actor; `ModelRulesTests` in the unit tests prove the hierarchy and owned-type rules on a connection-less model.

## Actors

`IActorContext` (`Guid ActorId`, `bool IsAuthenticated`) in `Dewiride.Erp.BuildingBlocks.Application.Actors` names who is writing. `ActorIds.System` is `00000000-0000-0000-0000-000000000001` and `ActorIds.Anonymous` is `00000000-0000-0000-0000-000000000002`; both are distinguishable from any Entra object id.

| Implementation | Process | Resolution |
|---|---|---|
| `SystemActorContext` (`BuildingBlocks.Application.Actors`) | every host that calls `AddErpPersistence` without `AddErpEndpoints`: the test database host, the migrator | `(ActorIds.System, false)` |
| `HttpActorContext` (`internal`, `BuildingBlocks.Endpoints.Actors`) | the API host | from `IHttpContextAccessor`, once per scope: no `HttpContext` (a hosted service's own scope) → `(ActorIds.System, false)`; a principal that is not authenticated → `(ActorIds.Anonymous, false)`; an authenticated principal → the `http://schemas.microsoft.com/identity/claims/objectidentifier` claim, else the short `oid` claim, parsed as a `Guid` → `(objectId, true)`; an authenticated principal without a parseable object identifier → `InvalidOperationException` |

`AddErpPersistenceCore` registers `TryAddSingleton(TimeProvider.System)` and `TryAddScoped<IActorContext, SystemActorContext>()`; `AddErpEndpoints` calls `AddHttpContextAccessor()` and `services.Replace(ServiceDescriptor.Scoped<IActorContext, HttpActorContext>())`, which wins whichever of the two runs first. A test supplies its own actor by registering `IActorContext` before `AddErpPersistenceCore`, as `SampleDatabase` does with `TestActorContext`.

## Migrations and design time

From `backend/` after `dotnet tool restore` (`dotnet-ef` is a local tool in `.config/dotnet-tools.json`):

```bash
dotnet ef migrations add <Name> --context SystemInfoDbContext --project Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo --startup-project Hosts/Api/Dewiride.Erp.Host.Api --output-dir Persistence/Migrations
dotnet ef database update --context SystemInfoDbContext --project Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo --startup-project Hosts/Api/Dewiride.Erp.Host.Api
dotnet ef migrations has-pending-model-changes --context SystemInfoDbContext --project Modules/Platform/SystemInfo/Module/Dewiride.Erp.Modules.Platform.SystemInfo --startup-project Hosts/Api/Dewiride.Erp.Host.Api
```

- `dotnet ef` always runs the API host offline: `AddErpPlatform` sets the bootstrap setting `ERP_CONFIGURATION_SOURCE=LocalDevelopment` when `EF.IsDesignTime` is true, before `AddErpConfiguration` runs, so a design-time host never contacts Azure App Configuration and reads the connection string from user secrets (developer machine) or the environment variable (CI). `LocalDevelopment` forces `appsettings.json` + user secrets + environment in any `ASPNETCORE_ENVIRONMENT`.
- No module carries an `IDesignTimeDbContextFactory`: the host composition builds every module context with exactly the run-time options (schema, history table, provider, conventions), and a per-module factory would duplicate that wiring and drift from it. The one factory in the repository is the test-only `SampleDbContextDesignTimeFactory` of the BuildingBlocks integration tests, whose `SampleDbContext` is not in the catalogue and whose test executable hosts no API.
- Migration SQL must run unchanged on SQL Server 2025 and Azure SQL: no `USE`, no cross-database names, no SQL Agent, no server-level permissions.
- Migrations are never applied at startup (`Migrate()`/`EnsureCreated()` are forbidden). Local development applies them with `dotnet ef database update`, which creates the `ErpAiPro` database when it does not exist; production applies them with the migrator from the migration-tooling sub-phase.

## Test databases

`SqlTestDatabase` in `Tests/Shared/Dewiride.Erp.Testing` is an xunit v3 assembly fixture, declared with `[assembly: AssemblyFixture(typeof(SqlTestDatabase))]` in every SQL-backed test project (`Tests/Host`, the SystemInfo integration tests, `Tests/Architecture`, the BuildingBlocks integration tests):

- it reads `ERP_TEST_SQL_CONNECTION`, a server-level connection string with rights to create databases and no `Database=` part (on the owner's machine `Server=localhost;Integrated Security=True;Encrypt=True;TrustServerCertificate=True`), and fails with a message naming the variable and `docs/guides/testing.md` when it is missing;
- it drops leftover databases named `ErpAiProTest_%` older than 24 hours, creates `ErpAiProTest_<yyyyMMddHHmmss>_<8 hex>` and migrates every catalogue context through `TestDatabaseHost`, a non-started host built with `AddErpPlatform`;
- it exposes the static `Current`; on dispose it clears the connection pools, sets `SINGLE_USER WITH ROLLBACK IMMEDIATE` and drops the database.

`ErpApiFactory` injects `Current.ConnectionString` as `Erp:Platform:Database:ConnectionString` unless the test supplied one with `WithConfiguration`, and throws a message naming the `AssemblyFixture` line when no test database exists, so a Development test host can never reach the developer's own `ErpAiPro` database through the user-secrets value. Every test host registers the readiness checks `self`, `database:platform_idempotency` and `database:platform_system_info`.

## CI

The composite action `.github/actions/start-sql-server` runs `docker run` of `mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-24.04@sha256:2b5b581621126574f3d1f75e78d3eebe8d05aedb59ad0cfdf9aa42cb0634d726` with an `sa` password generated at run time and masked in the log, and outputs `server-connection-string`, `database-connection-string` (`Database=ErpAiPro`) and `container-connection-string` (`Server=host.docker.internal,1433;Database=ErpAiPro;...`). ADR-0013 records why a composite `docker run` action is used rather than a `services:` container.

| Workflow | Use of the outputs |
|---|---|
| `ci-backend.yml` | `ERP_TEST_SQL_CONNECTION` on the test step; a `dotnet ef migrations has-pending-model-changes` step per context |
| `e2e.yml` | `dotnet ef database update` before the API starts; `Erp__Platform__Database__ConnectionString` on both API starts (the normal pair and the gated pair) |
| `docker-build.yml` | `dotnet ef database update` from the runner, then the compose secret file `infra/compose/secrets/Erp__Platform__Database__ConnectionString` written from `container-connection-string` before `compose config` and the smoke test |

## Schema ownership

- A module schema is `<domain>_<module>`: the descriptor's `Schema`, the constant passed to `AddModuleDbContext`, and the schema of the module's migrations history table are one value (`platform_system_info` for Platform / SystemInfo). `ModuleCatalog` rejects two modules sharing a schema.
- A schema owned by a building block (later: `platform_idempotency`, `files`) is named by its concern and sits outside the `<domain>_<module>` descriptor rule.

## Rules the architecture tests assert

`PersistenceTests` in `Tests/Architecture` fail the build when:

- a DbContext in a module assembly does not derive from `ModuleDbContext` or does not live in the namespace `<Module>.Persistence`;
- a catalogue context maps an entity type from another assembly;
- a module context's schema differs from its descriptor's `Schema`;
- a strongly-typed id property in a catalogue model is mapped without `StronglyTypedIdConverter`.
