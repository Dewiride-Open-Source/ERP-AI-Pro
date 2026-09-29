# Module anatomy

A module is a bounded context inside a domain. The Finance / Sales module shows the full shape of a Community module; every module uses the same one, omitting folders it does not need, and only an [Enterprise module](#enterprise-modules) adds `Ai/` folders.

## Backend

```
backend/Modules/Finance/Sales/
├── README.md                                                   what the module owns, its public contracts, events published and consumed
├── Contracts/Dewiride.Erp.Modules.Finance.Sales.Contracts/     the ONLY assembly other modules may reference
│   ├── SalesPermissions.cs                                      "finance.sales.invoices.issue" ...
│   └── Invoices/{ISalesInvoiceQueries.cs, InvoiceSummary.cs, Events/InvoiceIssued.cs}
├── Module/Dewiride.Erp.Modules.Finance.Sales/                   one implementation assembly, everything internal
│   ├── SalesModule.cs                                           sealed IModule: descriptor, AddServices (calls AddValidation for this assembly), MapEndpoints
│   ├── Persistence/{SalesDbContext.cs, Migrations/}
│   ├── Invoices/                                                FEATURE
│   │   ├── Domain/{Invoice.cs, InvoiceId.cs, InvoiceLine.cs, InvoiceNumber.cs, InvoiceStatus.cs, InvoiceErrors.cs, Events/, Rules/}
│   │   ├── Application/
│   │   │   ├── Commands/IssueInvoice/{IssueInvoiceCommand.cs, IssueInvoiceHandler.cs}
│   │   │   ├── Queries/GetInvoice/{GetInvoiceQuery.cs, GetInvoiceHandler.cs, InvoiceDetails.cs}
│   │   │   └── EventHandlers/InvoiceIssuedHandler.cs
│   │   ├── Endpoints/{InvoiceEndpoints.cs, Requests/CreateDraftInvoiceRequest.cs, Responses/InvoiceResponse.cs}
│   │   ├── Persistence/{InvoiceConfiguration.cs, InvoiceLineConfiguration.cs}
│   │   ├── Hosting/InvoiceReminderService.cs                    hosted services that drive Application handlers (a BackgroundService)
│   │   └── Reports/InvoiceRegister/{InvoiceRegisterQuery.cs, InvoiceRegisterHandler.cs, InvoiceRegisterEndpoints.cs}
│   ├── CreditNotes/  Receipts/  NumberingSeries/  Exports/       same shape
│   ├── Shared/Gst/{GstRate.cs, TaxBreakup.cs, PlaceOfSupplyResolver.cs}   module-internal shared code, sub-foldered by topic
│   ├── Reports/Gstr1/                                           module-level reports spanning features
│   ├── Integration/{Consumers/ClientDeactivatedHandler.cs, Publishers/InvoiceIssuedPublisher.cs}   the only folder that touches other modules' events
│   └── PublicApi/SalesInvoiceQueries.cs                         implements the Contracts interfaces
└── Tests/
    ├── UnitTests/Dewiride.Erp.Modules.Finance.Sales.UnitTests/           mirrors the feature folders; Domain + Application; no database
    └── IntegrationTests/Dewiride.Erp.Modules.Finance.Sales.IntegrationTests/   endpoints through WebApplicationFactory against SQL Server
```

Rules that keep the shape honest:

- Namespace equals folder path (build error otherwise); one type per file.
- A folder holds at most 12 source files; split by sub-feature or concern, never by growing the folder. `Persistence/Migrations/` is generated and exempt.
- `Domain/` references only `BuildingBlocks.Kernel` (and `SharedKernel`). `Application/` may use the module DbContext directly. `Endpoints/` call handlers and map results; no logic.
- A handler resolved from the container is the pipeline around it (logging, validation, unit of work), so a command handler changes tracked entities and returns a `Result`; it never calls `SaveChangesAsync`, and its domain events are dispatched inside the same transaction (`application-pipeline.md`).
- `Application/EventHandlers/<Event>/` holds `IDomainEventHandler<TEvent>` implementations; the same assembly scan registers them.
- `Persistence/<Module>DbContext` derives from `ModuleDbContext` and is registered from `AddServices` with `builder.AddModuleDbContext<T>(schema)`; there is no design-time factory, `dotnet ef` runs the API host offline (see `persistence.md`).
- Entities implement `IAuditable`, `ISoftDeletable` and `IVersioned` from `BuildingBlocks.Kernel.Domain` (getter-only, `private set` on the entity; on the root of an inheritance hierarchy, never on an owned type) to opt into the audit stamps of `AuditingSaveChangesInterceptor`, the `SoftDelete` query filter and the `rowversion` concurrency token; nothing is configured per entity. Money properties are `Money` values from `BuildingBlocks.Kernel.Monetary`, mapped by convention to `<Property>_Amount decimal(19,4)` and `<Property>_Currency char(3)`.
- `Hosting/` holds hosted services (`BackgroundService`) that drive Application handlers; it may reference Application, Domain, `BuildingBlocks.*` and `Microsoft.Extensions.*` (hosting, dependency injection, logging, feature management), never ASP.NET Core, EF Core or Endpoints.
- Request and response records carry unique names (`CreateDraftInvoiceRequest`, `InvoiceResponse`) so OpenAPI schema ids never collide.
- Request records are `public sealed record` types in `Endpoints/Requests/` with every attribute on the `property:` target (`FromQuery(Name = …)` for the wire name, `Description` for the document, DataAnnotations for the rules), and `AddServices` calls `builder.Services.AddValidation()`, because the validation generator skips non-public types and assemblies that never call it, without a warning (`http-conventions.md`). Response records stay `internal`.

### Enterprise modules

An Enterprise module ([ADR-0030](../adr/0030-community-and-enterprise-editions.md)) lives in the private repository `Dewiride-Enterprise/ERP-AI-Pro-Enterprise` and has the same four-project anatomy, folders and rules under the root namespace `Dewiride.Erp.Enterprise.Modules`: the assembly `Dewiride.Erp.Enterprise.Modules.<Domain>.<Module>`, its `.Contracts` assembly and its two test projects named after it. It references only Community `*.Contracts`, `BuildingBlocks.*` and Enterprise `*.Contracts` ([dependency rules](dependency-rules.md#editions)), so it reaches a Community module's data only through that module's contracts and integration events; its schema, route group, feature flag and permissions follow the [naming table](naming-and-namespaces.md) and are unique across both editions.

Every AI feature lives in an Enterprise module. The AI companion of a Community module is the Enterprise module `<Domain>/<Module>Ai` in the same domain, and it holds the `Ai/` folders: one per capability under the feature it serves, and a module-level `Ai/` for the tools it exposes to the assistant.

```
Finance/SalesAi/                                                 an Enterprise module, in Dewiride-Enterprise/ERP-AI-Pro-Enterprise
├── README.md
├── Contracts/Dewiride.Erp.Enterprise.Modules.Finance.SalesAi.Contracts/
├── Module/Dewiride.Erp.Enterprise.Modules.Finance.SalesAi/
│   ├── SalesAiModule.cs                                         sealed IModule; every AI capability a ModuleCapability with EnabledByDefault: false
│   ├── Invoices/                                                FEATURE
│   │   └── Ai/
│   │       ├── InvoiceExtraction/{ExtractInvoiceDraftCommand.cs, ExtractInvoiceDraftHandler.cs, InvoiceDraftExtraction.cs, extract-invoice.prompt.md}
│   │       └── SacSuggestion/{SuggestSacCodeQuery.cs, SuggestSacCodeHandler.cs, suggest-sac.prompt.md}
│   └── Ai/Tools/SalesAgentTools.cs                              module-level AI (tools exposed to the assistant)
└── Tests/
    ├── UnitTests/Dewiride.Erp.Enterprise.Modules.Finance.SalesAi.UnitTests/
    └── IntegrationTests/Dewiride.Erp.Enterprise.Modules.Finance.SalesAi.IntegrationTests/
```

`Ai/` folders exist only in Enterprise modules. They depend on `IChatClient` and `BuildingBlocks.Ai` only, and their output is a suggestion a human confirms ([AI capabilities](ai-capabilities.md)).

## Frontend

```
frontend/apps/web/src/features/finance/
├── _shared/                          finance-wide UI and helpers, imported by relative path from features/finance/** only
└── sales/
    ├── index.ts                      public surface: the only file app/ and other modules import
    ├── nav.ts                        navigation manifest salesNavigation { id, title, basePath, featureFlag, permission? }
    ├── invoices/
    │   ├── components/{invoices-overview.tsx, invoices-list.tsx, invoice-form.tsx, invoice-lines-editor.tsx, invoice-status-badge.tsx, invoices-overview-skeleton.tsx}
    │   ├── server/{actions.ts, queries.ts}
    │   ├── forms/invoice-form.schema.ts
    │   ├── lists/invoices.list.ts
    │   └── hooks/use-invoice-draft.ts
    └── credit-notes/  receipts/  gstr1-report/   same shape

frontend/apps/web/src/app/(app)/finance/sales/layout.tsx                                                              gates the segment with requireFeature
frontend/apps/web/src/app/(app)/finance/sales/invoices/{page.tsx, loading.tsx, new/page.tsx, [invoiceId]/page.tsx}   compose only
frontend/e2e/tests/finance/sales/invoices.spec.ts                                                                     one spec per feature
```

Rules that keep the shape honest (ADR-0024; the full register with its enforcers is in `dependency-rules.md`, checked by `node scripts/checks/feature-boundaries.ts` and `pnpm lint`):

- `index.ts` re-exports what `app/` renders (page components, loading skeletons) and the `nav` manifest, never Server Functions, queries, schemas or list definitions (rule I7: it imports nothing from `server/`, `forms/` or `lists/`). Another module imports only `@/features/finance/sales`; inside the module, features import each other by relative path, never through the alias. A page that imports one page component from `index.ts` bundles only that component's client code, because `apps/web/package.json` declares every web source file but `instrumentation-client.ts` and stylesheets free of side effects ([dependency rules](dependency-rules.md#frontend)); a file whose evaluation does more than define exports joins that `sideEffects` list.
- `nav.ts` exports one `as const` object of the `NavigationEntry` shape (`shared/layout/navigation-entry.ts`: `id`, `title`, `basePath` typed `Route`, `featureFlag`, optional `permission`), re-exported by `index.ts` and listed in `features/registry.ts`, which only `app/` imports.
- A module root holds `index.ts`, `nav.ts` and feature folders; a feature holds only `components/`, `server/`, `forms/`, `lists/`, `hooks/` and `ai/`, and `ai/` appears only in the features of an [Enterprise module](#enterprise-modules) (`features/finance/sales-ai/invoices/ai/invoice-extraction/` holds the review form of the invoice extraction). `forms/`, `lists/`, `hooks/` and `ai/` are created with their first real file (in an Enterprise feature, the first AI capability behind its own flag brings the first `ai/<capability>/`), never as empty folders; the modules today use `components/` and `server/`, `platform/design/form-kit` also `forms/`, and `platform/attachments/files`, `platform/design/data-table` and `platform/design/feedback` also `lists/`.
- `server/actions.ts` opens with `"use server"`; `server/queries.ts` imports `"server-only"`; `forms/` holds `<name>.schema.ts` files, `lists/` `<name>.list.ts` files and `hooks/` `use-<name>.ts(x)` files, plus a `.test.ts` only beside the file it tests.
- A mutation form is three files (ADR-0025; the working example is `features/platform/design/form-kit`):
  - `forms/<name>.schema.ts`, isomorphic and the form's whole contract: the `z.object` schema built from `@/shared/forms/schemas/*` (`requiredText`, `amountSchema`, `calendarDateSchema`, `dateRangeSchema`, `optionalInput`, `gstinSchema`, `panSchema`, `ifscSchema`), `<Name>Values = z.input<…>` and `<Name> = z.output<…>`, the default values, named limits that mirror the request record, and, when needed, `<name>FieldAliases` (API key → form path, typed `FieldAliases`) and `<name>ProblemMessages` (problem code → sentence, typed `ProblemMessages`). Fields are named after the API request's JSON members so most error keys need no alias. No `server-only`, no value import from `@/shared/api`, no asynchronous refinement; messages are plain English for the person. `<name>.schema.test.ts` beside it runs under `node --test`.
  - `components/<name>-form.tsx` (`"use client"`): `useActionForm({ schema, action, defaultValues, idempotent })` from `@/shared/forms/client/use-action-form`, a `<form noValidate method="post" onSubmit={onSubmit}>` without an `action` prop, `FormAlert` fed by `alert` and `alertRef`, one `FormField` per field with `id={fieldId("<path>")}` and `errors={fieldErrorMessages(…)}`, `register` for a plain input and a `Controller` (one per leaf value) for a composite input, and `SubmitButton` with `pending` and `ready`. A date input's controller receives `onBlur` only when focus leaves its text boxes, its button and its open calendar together, and a date range passes `ends` so only the end with its own message is marked invalid (the form kit's `leavesGroup` and `rangeEndState`).
  - `server/actions.ts`: `export async function <verb><Thing>(previous: FormState, submission: unknown): Promise<FormState>` that re-parses with `parseSubmission(schema, submission, { idempotent })`, maps the command to the generated client's request model in a non-exported synchronous function, calls the API through `callApi` (with `Idempotency-Key` for a create), returns `problemToFormState(error, { aliases, messages })` for a caught error, and redirects outside the `try` or returns `formSucceeded(…)`. The session check goes before `parseSubmission` from `authentication-web-login-page-and-session-integration` on, and the permission check beside it from `user-management-roles-and-permissions` on. A file is never sent to a Server Function; the form holds the attachment id the browser upload returned.
- A list is three files (ADR-0026; the working examples are `features/platform/design/data-table` and `features/platform/attachments/files`):
  - `lists/<name>.list.ts`: `export const <name>List = defineList({ pageSizes, defaultPageSize, sortFields, defaultSort, filters, columns, hideableColumns })` from `@/shared/lists/list-definition`, pure data shared by the server and the browser; `defineList` refuses at import a definition the API cannot serve.
  - `components/<name>-overview.tsx` (a Server Component, exported through `index.ts`, given the search parameters the route's `page.tsx` awaited): `readListQuery(searchParameters, definition)`, `redirect(listHref(basePath, query, definition))` when the address is not canonical, the page from `server/queries.ts` called with `listApiParameters(query, definition)`, a redirect to the last page when `page` is past it, then the list component with the query and the page.
  - `components/<name>-list.tsx` (`"use client"`): the `DataTableColumn` array (column ids are the API's sort fields), the filter controls of `@dewiride/erp-ui/components/data-table/filters/*` named after the definition's parameters with their values from `query.filters`, and `ListTable` from `@/shared/lists/list-table` with `basePath`, `definition`, `query`, `page`, `label`, `noun`, `empty`, `noMatches` and, when rows can be selected, `selection`.
- Route files only compose. A page renders one export of the module's `index.ts`; the segment `layout.tsx` gates it with `requireFeature`; `loading.tsx` renders the module's skeleton (`<InvoicesOverviewSkeleton />`, from `components/invoices-overview-skeleton.tsx`, exported through `index.ts`, a `LoadingStatus` with `pageTitle` naming the page around `Skeleton`s), and every page that takes noticeable time to prepare has one. It sits below the segment `layout.tsx`, never above it: the shell has no `loading.tsx`, because a loading boundary around the gating layout would turn a disabled module's 404 and a page's canonical-address redirect into a streamed 200 ([ADR-0027](../adr/0027-feedback-motion-and-the-accessibility-baseline.md)). Any other UI a segment needs lives in the feature and is exported the same way, because a file in `app/` renders no HTML element.
- A page or layout that reads `params` or `searchParams` types its props with the generated `PageProps<"/finance/sales/invoices/[invoiceId]">` or `LayoutProps<…>`.

## Identity of a module

| Aspect | Value for Finance / Sales |
|---|---|
| Assembly | `Dewiride.Erp.Modules.Finance.Sales` |
| Schema | `finance_sales` |
| Route group | `/api/finance/sales` |
| Feature flag | `Erp.Modules.Finance.Sales` |
| Permissions | `finance.sales.<feature>.<action>` |
| Configuration keys | `Erp:Finance:Sales:<Setting>` |
| Frontend | `features/finance/sales/`, routes `/finance/sales/...` |
