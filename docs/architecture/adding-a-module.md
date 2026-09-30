# Adding a module

Use `backend/Modules/Platform/SystemInfo` as the reference implementation. A scaffolder (`scripts/new-module`) arrives with the business building blocks phase; until then follow these steps by hand.

## Edition

Every module belongs to one edition. Decide it by [ADR-0030](../adr/0030-community-and-enterprise-editions.md) and have the owner confirm it before the module's first sub-phase starts: `node scripts/roadmap/roadmap.ts edition <community|enterprise> <id…> --confirm` records the confirmation, and `start` refuses a sub-phase whose edition is not confirmed.

- A Community module is built in this repository by the steps below.
- An Enterprise module is built by the same steps in the private repository `Dewiride-Enterprise/ERP-AI-Pro-Enterprise`, under the root namespace `Dewiride.Erp.Enterprise.Modules` (`Dewiride.Erp.Enterprise.Modules.<Domain>.<Module>` in place of `Dewiride.Erp.Modules.<Domain>.<Module>` in every project name below), and references only Community `*.Contracts`, `BuildingBlocks.*` and Enterprise `*.Contracts` ([dependency rules](dependency-rules.md#editions), [Enterprise modules](module-anatomy.md#enterprise-modules)). Nothing in this repository names it: `Modules.All` lists Community modules only, and the Enterprise solution and hosts that compose Enterprise modules arrive with the roadmap item `business-building-blocks-enterprise-edition-composition`.

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

1. Create `frontend/apps/web/src/features/<domain>/<module>/` with `index.ts` (the public surface: the components `app/` renders and the `nav` manifest, never Server Functions, queries or schemas) and `nav.ts` (an `as const` `<module>Navigation` object of the `NavigationEntry` shape with the `featureFlag` that reveals it and the optional `permission`, which filters the navigation from `user-management-roles-and-permissions` on). Put each feature in its own folder holding only `components/`, `server/`, `forms/`, `lists/`, `hooks/` and, in an Enterprise module, `ai/` (`module-anatomy.md`); features of the module import each other by relative path.
2. Register the manifest in `features/registry.ts`, importing it from the module's `index.ts`.
3. Add routes under `app/(app)/<domain>/<module>/...`. Route files only compose and import only the module's `index.ts`, `@/features/registry`, `@/shared/**`, `next` and `react`: gate the segment with `await requireFeature(<module>Navigation.featureFlag)` in its `layout.tsx`, so a disabled module renders the in-shell not-found page; each `page.tsx` renders one export of the module's `index.ts`; `loading.tsx` renders the module's skeleton (`components/<name>-skeleton.tsx`, exported through `index.ts`, wrapped in `LoadingStatus` with `pageTitle` naming the page), and every page that takes noticeable time to prepare has one, below the segment's gating `layout.tsx`, because the shell has no loading status of its own and a navigation otherwise keeps the old page on screen until the new one is ready ([ADR-0027](../adr/0027-feedback-motion-and-the-accessibility-baseline.md)); any other UI a segment needs lives in the feature and is exported the same way. A page or layout that reads `params` or `searchParams` types its props with `PageProps<"/<route>">` or `LayoutProps<"/<route>">`.
4. Run `node scripts/checks/feature-boundaries.ts` from the repository root and `pnpm lint` and `pnpm typecheck` from `frontend/`: they check the module's folders and files, its imports in both directions and every route it links to (the rule register is in `dependency-rules.md`).
5. Add page objects and specs under `frontend/e2e/{pages,tests}/<domain>/<module>/`.

## Splitting a module

When a module outgrows its schema or team ownership, the feature becomes its own module: new four-project set, new schema, data moved by a forward migration, contracts introduced for what the old module still needs, and the old code deleted in the same pull request. No compatibility shims.
