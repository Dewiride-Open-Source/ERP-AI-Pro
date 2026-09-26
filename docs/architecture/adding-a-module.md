# Adding a module

Use `backend/Modules/Platform/SystemInfo` as the reference implementation. A scaffolder (`scripts/new-module`) arrives with the business building blocks phase; until then follow these steps by hand.

## Backend

1. Create `backend/Modules/<Domain>/<Module>/` with `README.md` and four projects:
   - `Contracts/Dewiride.Erp.Modules.<Domain>.<Module>.Contracts/` — class library; references `Dewiride.Erp.BuildingBlocks.Kernel`.
   - `Module/Dewiride.Erp.Modules.<Domain>.<Module>/` — class library with `FrameworkReference Microsoft.AspNetCore.App`; references its Contracts, `Dewiride.Erp.BuildingBlocks.Persistence` and the other BuildingBlocks it needs.
   - `Tests/UnitTests/Dewiride.Erp.Modules.<Domain>.<Module>.UnitTests/` and `Tests/IntegrationTests/Dewiride.Erp.Modules.<Domain>.<Module>.IntegrationTests/` — test projects (the `build/Tests.props` defaults apply by the `Tests` suffix); the integration project references `Tests/Shared/Dewiride.Erp.Testing` and `Hosts/Api`.
2. Add `<Module>Module.cs` implementing `IModule`: the `ModuleDescriptor` (domain, module, schema, route prefix, feature flag, permissions, capabilities), `AddServices` (options, DbContext, handlers, `AddValidation()`) and `MapEndpoints(RouteGroupBuilder)`. The module flag `Erp.Modules.<Domain>.<Module>` gates the whole route group and evaluates enabled until a configuration source switches it off; each `ModuleCapability(Name, EnabledByDefault)` becomes the flag `Erp.Modules.<Domain>.<Module>.<Name>` (declare AI capabilities with `EnabledByDefault: false`).
3. Add `<Module>DbContext` under `Persistence/` deriving from `ModuleDbContext`, with the schema constant `SchemaName = "<domain>_<module>"`; register it in `AddServices` with `builder.AddModuleDbContext<<Module>DbContext>(<Module>DbContext.SchemaName)` and set `Schema` on the descriptor to the same constant. There is no design-time factory: `dotnet ef` runs the API host offline (`persistence.md`). Create the first migration from the repository root after `cd backend && dotnet tool restore`; `scripts/ef/ef.ts` discovers the new context by itself, and the migrator, the test databases and CI pick it up through the catalogue and the same discovery ([migrations guide](../guides/migrations.md)):

   ```bash
   node scripts/ef/ef.ts add Initial<Aggregate>s --context <module-key>
   ```

   Reference data the module needs goes in an `ISeeder` registered with `builder.Services.AddSeeder<TSeeder>(order)`, idempotent and tested by running it twice.

4. Add feature folders following `module-anatomy.md`.
5. Register the module in `Modules.All` (`backend/Hosts/Composition/Dewiride.Erp.Host.Composition`, one line) and add the four projects to `backend/Dewiride.Erp.slnx` under solution folder `Modules/<Domain>/<Module>` and to `backend/solutions/<Domain>.slnf`.
6. Add the module's routes to `docs/routing.md` and its configuration keys and feature flag to `docs/configuration.md`.
7. Run the architecture tests: they verify the module is listed, its four projects and `README.md` exist under `Modules/<Domain>/<Module>/`, its namespaces match folders, no folder holds more than 12 source files, its descriptor follows the naming table (permissions start with `<domain>.<module>.`), its endpoints require authorization, it references only contracts of other modules, its DbContext derives from `ModuleDbContext` in the namespace `<Module>.Persistence`, maps only entity types from its own assembly with a schema equal to the descriptor's `Schema`, and maps every strongly-typed id through `StronglyTypedIdConverter`.

## Frontend

1. Create `frontend/apps/web/src/features/<domain>/<module>/` with `index.ts` (public surface) and `nav.ts` (navigation manifest with the `featureFlag` that reveals it and, from the authentication phase, the permission). Gate the module's route segment with `await requireFeature(<module>Navigation.featureFlag)` in its `layout.tsx`, so a disabled module renders the in-shell not-found page.
2. Register the manifest in `features/registry.ts`.
3. Add routes under `app/(app)/<domain>/<module>/...` that import only from the module `index.ts`.
4. Add page objects and specs under `frontend/e2e/{pages,tests}/<domain>/<module>/`.

## Splitting a module

When a module outgrows its schema or team ownership, the feature becomes its own module: new four-project set, new schema, data moved by a forward migration, contracts introduced for what the old module still needs, and the old code deleted in the same pull request. No compatibility shims.
