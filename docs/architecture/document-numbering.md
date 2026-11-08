# Document numbering

In one paragraph: a module declares each kind of document it numbers; Finance or an administrator sets the kind's numbering up on `/platform/numbering` (the fixed text a number begins with, the financial year as `2026-27`, `26-27` or not at all, a separator after the year, the digits of the running number, and the next number of the current year); the command that issues a document asks for its number last, in its own unit of work, and the number commits with the document or not at all, so each financial year's numbers run from its first without a gap or a repeat ([ADR-0066](../adr/0066-document-numbering-series-per-kind-of-document-fixed-for-a-year-at-its-first-number-and-taken-in-the-issuing-transaction.md)).

| Part | Where | What it does |
|---|---|---|
| Building block | `backend/BuildingBlocks/Numbering` (`AddErpNumbering()`, called by `AddErpPlatform` for every host) | the catalogue of the kinds of document, `IDocumentNumbers`, `INumberingAdministration`, `NumberingDbContext` (schema `platform_numbering`), the record names |
| Module | `backend/Modules/Platform/Numbering` (schema `null`) | the four routes under `/api/platform/numbering`, their handlers and `ISerialNumberRules`, read from the statutory parameter set `gst.document-serial-numbers` |
| Web | `frontend/apps/web/src/features/platform/numbering`, route `/platform/numbering` | each kind of document with its numbering and its years, Set up and Change |

## Declaring a kind of document

```csharp
builder.Services.AddNumberedDocument(new NumberedDocument(
    "finance.sales.tax-invoices",        // three lower-case kebab segments, by convention the module's permission prefix first
    "Tax invoices",                      // what the documents are called, plural
    "Invoices issued for taxable supplies.",
    "46(b)"));                           // the clause of gst.document-serial-numbers its numbers follow
```

The key names the kind in its numbering's row and never follows a class name; a key declared twice, malformed or with a blank title, description or rule fails when the host is built. Its first two segments are by convention the declaring module's permission prefix, as a change key's are; nothing checks that, because a key only names a numbering and the catalogue refuses a key declared twice.

## Numbering a document

The module's context maps the numbering's tables, left out of its migrations by their configurations:

```csharp
public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();

public DbSet<NumberSeriesYear> NumberSeriesYears => Set<NumberSeriesYear>();
```

and the command that issues the document asks for the number last, after its own checks:

```csharp
var number = await numbers.NextAsync(context, "finance.sales.tax-invoices", invoice.IssueDate, cancellationToken);
if (number.IsFailure)
{
    return number.Error!;
}

invoice.Issue(number.Value.Text, number.Value.Year, number.Value.Sequence);
```

`NextAsync` takes the application lock `platform_numbering.<key>` of the unit of work's transaction (10 seconds; busy answers 409 `concurrency.conflict`), reads the kind's numbering and the year of the document's date, opens the year at 1 in the numbering's format when it is not open (refused with 422 `numbering.no-rule` when the ERP holds no rule for the year, or `numbering.format-not-allowed` when the format no longer meets it), gives the year's next number and moves it on. The lock ends with the transaction, so the next save of that kind waits until this document commits or rolls back and then reads the year's next number as it stands. A kind that is not set up answers 422 `numbering.not-set-up`, and a year whose numbers no longer fit the most characters it opened with answers 422 `numbering.exhausted`. Asking outside a transaction, on a context that maps no numbering, or for a kind no module declares throws: each is a mistake in the issuing module.

`DocumentNumber(Text, Year, Sequence)` is what the document keeps: the number as it is printed, unique within its financial year, and the running number by which the year's documents sort.

## Formats and years

| Rule | Where it is held |
|---|---|
| A number is the prefix, then the year and the separator when the year is shown, then the running number padded with zeros to the digits; a longer running number keeps its digits | `NumberFormat.Render` |
| At most the rule's characters, each a capital letter, a digit or one of the rule's special characters, the year's "-" included; never beginning with "0", "/" or "-" | `NumberFormat.ProblemsUnder`, keyed `Format.Prefix`, `Format.Year`, `Format.Separator`, `Format.Digits` or `Format` |
| The last running number that fits | `NumberFormat.LastNumberWithin` |
| Each year keeps the format, the most characters and the first number it opened with, and moves its next number on | `NumberSeriesYear` (unique per numbering and year) |
| Setting up opens the current year at the next number asked; a change reformats the current year and may move its next number while it has given no number; a year that has given one keeps both, and the format applies from the next year | `INumberingAdministration` (`SetUpAsync`, `ChangeAsync`, each in a unit of work of its own on `NumberingDbContext` under the same lock) |

## Records

`NumberSeries` is recorded under `platform.numbering.series` and `NumberSeriesYear` under `platform.numbering.series-years`, filed under its numbering. The next number is left out of the change records, so opening a year writes one record and the numbers given write none; the document that carries a number records it.

## Tests

| Where | What |
|---|---|
| `Tests/BuildingBlocks/…UnitTests/Numbering` | the format's rendering, last number and problems; a year's numbers and reopening; the catalogue |
| `Tests/BuildingBlocks/…IntegrationTests/Numbering` | numbers taken in the sample context's transactions: consecutive, forty saves at once with every fourth rolled back after its save (exactly 1 to 30), a failed unit of work giving its number back, a new year at 1, the refusals, the change records; the administration over the shared database, with a made-up rule |
| `Modules/Platform/Numbering/Tests` | the handlers (unit); every route's permission and the feature gate, the set-up, change and refusals, the rule from the parameter store (integration) |
| `Tests/Architecture` | `NumberingTests`: every context maps the tables as their owner keeps them |
| `frontend/e2e/tests/platform/numbering` | on every project in light and dark: the page as an accountant given the Finance role (a set-up after a refused format, a change before a year gives a number, a year that has given numbers keeping its format while a change applies from the next year, a year that used every number that fits, the Finance role taken away while the dialog is open) and as an administrator; the routes refused to an employee; the module switched off; the API unavailable |

The tests and the end-to-end host declare sixteen kinds of their own (`TestNumberedDocuments`), and a test that changes a numbering takes a kind no other test running beside it takes; the end-to-end host's test routes `POST /api/__test/numbering/issues` and `POST /api/__test/numbering/resets` take a kind's numbers through `IDocumentNumbers` and remove its numbering.
