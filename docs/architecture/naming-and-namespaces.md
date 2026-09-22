# Naming and namespaces

## Backend

| Thing | Pattern | Example |
|---|---|---|
| Building block assembly | `Dewiride.Erp.BuildingBlocks.<Concern>` | `Dewiride.Erp.BuildingBlocks.Endpoints` |
| Host assembly | `Dewiride.Erp.Host.<Name>` | `Dewiride.Erp.Host.Api` |
| Module assembly | `Dewiride.Erp.Modules.<Domain>.<Module>` | `Dewiride.Erp.Modules.Finance.Sales` |
| Contracts assembly | `Dewiride.Erp.Modules.<Domain>.<Module>.Contracts` | `Dewiride.Erp.Modules.Finance.Sales.Contracts` |
| Test assemblies | `<assembly>.UnitTests`, `<assembly>.IntegrationTests` | `Dewiride.Erp.Modules.Finance.Sales.UnitTests` |
| Namespace | assembly name + folder path | `Dewiride.Erp.Modules.Finance.Sales.Invoices.Domain` |
| Module class | `<Module>Module` | `SalesModule` |
| DbContext | `<Module>DbContext` | `SalesDbContext` |
| Command / query | `<Verb><Noun>Command`, `Get<Noun>Query`, `List<Noun>Query` | `IssueInvoiceCommand`, `ListInvoicesQuery` |
| Handler | `<Command or Query name without suffix>Handler` | `IssueInvoiceHandler` |
| Endpoints class | `<Feature>Endpoints` | `InvoiceEndpoints` |
| Request / response | `<UseCase>Request`, `<Noun>Response` | `CreateDraftInvoiceRequest`, `InvoiceResponse` |
| Strongly-typed id | `<Entity>Id` record struct | `InvoiceId` |
| Error catalogue | `<Entity>Errors` static class | `InvoiceErrors.AlreadyIssued` |
| Options | `<Module>Options` bound from `Erp:<Domain>:<Module>` | `SalesOptions` |
| Prompt file | `<capability>.prompt.md` next to its handler | `suggest-sac.prompt.md` |
| Route | `/api/<domain>/<module>/<resource>[/{id}[/<action>]]` kebab-case, plural | `POST /api/finance/sales/invoices/{invoiceId}/issue` |
| Schema | `<domain>_<module>` | `finance_sales` |
| Table | plural PascalCase | `Invoices`, `InvoiceLines` |
| Column | PascalCase, PK `Id`; complex-type members `<Property>_<Member>` | `Name`, `Price_Amount`, `Price_Currency`, `Address_City` |
| Audit interfaces | `IAuditable`, `ISoftDeletable`, `IVersioned` in `Dewiride.Erp.BuildingBlocks.Kernel.Domain`; getter-only, `private set` on the entity | `sealed class Client : AggregateRoot<ClientId>, IAuditable, ISoftDeletable, IVersioned` |
| Soft-delete query filter | the constant `SoftDeleteFilter.Name` (`Dewiride.Erp.BuildingBlocks.Persistence.Conventions`) | `"SoftDelete"`; `query.IncludeDeleted()` |
| Money primitives | `Dewiride.Erp.BuildingBlocks.Kernel.Monetary` | `Money`, `Currency`, `Percentage` and their `[JsonConverter]`-attached `MoneyJsonConverter`, `CurrencyJsonConverter`, `PercentageJsonConverter` |
| Time primitives | `Dewiride.Erp.BuildingBlocks.Kernel.Time` | `FinancialYear`, `FinancialQuarter`, `IndianStandardTime`, `FinancialYearJsonConverter` |
| Actor | `IActorContext`, `ActorIds`, `SystemActorContext` in `Dewiride.Erp.BuildingBlocks.Application.Actors`; `HttpActorContext` in `Dewiride.Erp.BuildingBlocks.Endpoints.Actors` | `ActorIds.System`, `ActorIds.Anonymous` |
| Error code | `<area>.<kebab-case-problem>` in ProblemDetails `code` | `request.invalid`, `query.invalid-field`, `concurrency.conflict`, `idempotency.key-reused` |
| Permission | `<domain>.<module>.<feature>.<action>` | `finance.sales.invoices.issue` |
| Feature flag | `Erp.Modules.<Domain>.<Module>[.<Capability>]` | `Erp.Modules.Finance.Sales.EInvoicing` |
| Configuration key | `Erp:<Domain>:<Module>:<Setting>` | `Erp:Finance:Sales:InvoicePrefix` |
| Key Vault secret | `Erp--<Domain>--<Module>--<Name>` | `Erp--Finance--Sales--IrpClientSecret` |
| OpenTelemetry source | `Dewiride.Erp.<Domain>.<Module>` | `Dewiride.Erp.Finance.Sales` |
| Test method | `Method_Condition_ExpectedResult` | `Issue_WhenAlreadyIssued_ReturnsAlreadyIssuedError` |

Route exemptions: `/api/auth/*` (authentication endpoints owned by the host) and `/api/platform/*` (platform modules). Health endpoints are `/healthz/live` and `/healthz/ready`.

## Frontend

| Thing | Pattern | Example |
|---|---|---|
| Workspace package | `@dewiride/erp-<name>` | `@dewiride/erp-web`, `@dewiride/erp-ui` |
| Feature folder | `features/<domain>/<module>/<feature>/` kebab-case | `features/finance/sales/invoices/` |
| Route folder | mirrors the API resource | `app/(app)/finance/sales/invoices/[invoiceId]/page.tsx` |
| Component file | kebab-case, PascalCase export | `invoice-table.tsx` → `InvoiceTable` |
| Server Function file | `server/actions.ts` (one exported function per mutation) | `issueInvoice` |
| Query file | `server/queries.ts` | `getInvoice`, `listInvoices` |
| Schema file | `forms/<name>.schema.ts` | `invoice-form.schema.ts` |
| Hook | `use-<name>.ts` | `use-invoice-draft.ts` |
| Playwright spec | `e2e/tests/<domain>/<module>/<feature>.spec.ts` | `e2e/tests/finance/sales/invoices.spec.ts` |
| Page object | `e2e/pages/<domain>/<module>/<feature>.page.ts` | `e2e/pages/finance/sales/invoices.page.ts` |

## Git

- Branch `<type>/<sub-phase-id>[-<slug>]` — `feat/finance-sales-invoice-aggregate`.
- Commit `type(scope): imperative subject` — `feat(finance-sales): add invoice numbering series`; the body carries the sub-phase id.
