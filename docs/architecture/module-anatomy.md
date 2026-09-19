# Module anatomy

A module is a bounded context inside a domain. The Finance / Sales module shows the full shape; every module uses the same one, omitting folders it does not need.

## Backend

```
backend/Modules/Finance/Sales/
├── README.md                                                   what the module owns, its public contracts, events published and consumed
├── Contracts/Dewiride.Erp.Modules.Finance.Sales.Contracts/     the ONLY assembly other modules may reference
│   ├── SalesPermissions.cs                                      "finance.sales.invoices.issue" ...
│   └── Invoices/{ISalesInvoiceQueries.cs, InvoiceSummary.cs, Events/InvoiceIssued.cs}
├── Module/Dewiride.Erp.Modules.Finance.Sales/                   one implementation assembly, everything internal
│   ├── SalesModule.cs                                           sealed IModule: descriptor, AddServices, MapEndpoints
│   ├── SalesValidation.cs                                       services.AddValidation() for this assembly
│   ├── Persistence/{SalesDbContext.cs, SalesDbContextDesignTimeFactory.cs, Migrations/}
│   ├── Invoices/                                                FEATURE
│   │   ├── Domain/{Invoice.cs, InvoiceId.cs, InvoiceLine.cs, InvoiceNumber.cs, InvoiceStatus.cs, InvoiceErrors.cs, Events/, Rules/}
│   │   ├── Application/
│   │   │   ├── Commands/IssueInvoice/{IssueInvoiceCommand.cs, IssueInvoiceHandler.cs}
│   │   │   ├── Queries/GetInvoice/{GetInvoiceQuery.cs, GetInvoiceHandler.cs, InvoiceDetails.cs}
│   │   │   └── EventHandlers/InvoiceIssuedHandler.cs
│   │   ├── Endpoints/{InvoiceEndpoints.cs, Requests/CreateDraftInvoiceRequest.cs, Responses/InvoiceResponse.cs}
│   │   ├── Persistence/{InvoiceConfiguration.cs, InvoiceLineConfiguration.cs}
│   │   ├── Reports/InvoiceRegister/{InvoiceRegisterQuery.cs, InvoiceRegisterHandler.cs, InvoiceRegisterEndpoints.cs}
│   │   └── Ai/
│   │       ├── InvoiceExtraction/{ExtractInvoiceDraftCommand.cs, ExtractInvoiceDraftHandler.cs, InvoiceDraftExtraction.cs, extract-invoice.prompt.md}
│   │       └── SacSuggestion/{SuggestSacCodeQuery.cs, SuggestSacCodeHandler.cs, suggest-sac.prompt.md}
│   ├── CreditNotes/  Receipts/  NumberingSeries/  Exports/       same shape
│   ├── Shared/Gst/{GstRate.cs, TaxBreakup.cs, PlaceOfSupplyResolver.cs}   module-internal shared code, sub-foldered by topic
│   ├── Reports/Gstr1/                                           module-level reports spanning features
│   ├── Ai/Tools/SalesAgentTools.cs                              module-level AI (tools exposed to the assistant)
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
- `Ai/` folders depend on `IChatClient` and `BuildingBlocks.Ai` only; their output is a suggestion a human confirms.
- Request and response records carry unique names (`CreateDraftInvoiceRequest`, `InvoiceResponse`) so OpenAPI schema ids never collide.

## Frontend

```
frontend/apps/web/src/features/finance/sales/
├── index.ts                      public surface: the only file app/ may import
├── nav.ts                        navigation manifest { id, title, icon, basePath, permission, items }
├── invoices/
│   ├── components/{invoice-table.tsx, invoice-form.tsx, invoice-lines-editor.tsx, invoice-status-badge.tsx}
│   ├── server/{actions.ts, queries.ts}
│   ├── forms/invoice-form.schema.ts
│   ├── hooks/use-invoice-draft.ts
│   └── ai/{invoice-extraction/, sac-suggestion/}
├── credit-notes/  receipts/  reports/gstr1/   same shape
└── _shared/                      finance-wide UI, importable only from features/finance/**

frontend/apps/web/src/app/(app)/finance/sales/invoices/{page.tsx, new/page.tsx, [invoiceId]/page.tsx}   routes only
frontend/e2e/tests/finance/sales/invoices.spec.ts                                                        one spec per feature
```

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
