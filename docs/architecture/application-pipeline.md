# Application pipeline

Every use case is a command or a query handled by one class. There is no mediator: a handler is resolved from the container by its closed interface and called directly. What the container returns is not the handler itself but a pipeline that wraps it.

## Handlers

| Kind | Request | Handler | Result |
|---|---|---|---|
| Command (changes state) | `ICommand<TResult>` | `ICommandHandler<TCommand, TResult>` | `Task<Result<TResult>>` |
| Query (reads state) | `IQuery<TResult>` | `IQueryHandler<TQuery, TResult>` | `Task<Result<TResult>>` |
| Domain event reaction | `IDomainEvent` | `IDomainEventHandler<TEvent>` | `Task` |

`services.AddHandlersFromAssembly(assembly)` (called by every module's `AddServices`) registers the pipeline steps once, then, for every handler in the assembly, the concrete type as itself and its closed interface as a `CommandHandlerPipeline<TCommand, TResult>` or `QueryHandlerPipeline<TQuery, TResult>` carrying a `HandlerDescriptor(HandlerType, RequestType, Kind)`. Domain event handlers are registered as their closed interface, so a single event may have several. Handlers, their pipelines and the unit-of-work step are scoped; the stateless logging and validation steps are singletons.

## Steps

A step implements `IPipelineStep` and declares its `PipelineStage`. The order comes from the stage, never from registration order, so a step added later cannot reorder the pipeline by accident:

| Stage | Step | What it does |
|---|---|---|
| `Logging` | `LoggingStep` | starts an activity on the source `Dewiride.Erp.Application` named after the request, tags it with the request, handler and kind, logs the outcome (commands at information, queries at debug; a failed result logs its error code, an exception logs a warning and is rethrown) |
| `Validation` | `ValidationStep` | `Validator.TryValidateObject(validateAllProperties: true)` over the request, including `IValidatableObject`; a failure returns `Error.Validation("request.invalid", …)` whose `Fields` carry one message list per member, so nothing reaches the database |
| `UnitOfWork` | `UnitOfWorkStep` (`BuildingBlocks.Persistence`) | commands only, and only when the handler's assembly owns a context in the `DbContextCatalog`: runs the handler inside `EfUnitOfWork.RunAsync` on that context; queries pass straight through |

## Unit of work

`EfUnitOfWork.RunAsync` joins an ambient transaction when one exists. Otherwise it runs the whole use case through the provider's execution strategy: `ChangeTracker.Clear()`, a transaction, the handler, then a loop that takes the domain events off the tracked `IHasDomainEvents` aggregates, saves, dispatches them and saves again, until a round finds no event (at most ten, after which it throws naming the event types that cycle). The events are taken before the save, so an aggregate the handler removed still raises them even though EF detaches its entry once the row is gone. A successful result commits; a failed result rolls back; an exception propagates and rolls back. A `DbUpdateConcurrencyException` becomes `Error.Conflict("concurrency.conflict", …)` naming the entity types, which `ResultExtensions.ToProblem` answers as 409.

A handler therefore never calls `SaveChangesAsync`: it changes tracked entities and returns a `Result`.

## Results and ProblemDetails

`ResultExtensions` maps a `Result` to a typed HTTP result: `ToHttpResult()` (200 or 204), `ToCreatedResult(location)` (201 with a `Location` header) and `ToProblem()` for the failure. Every problem carries the `code` extension. A validation error carrying `Fields` becomes `HttpValidationProblemDetails`, so the body has an `errors` member per field:

| `ErrorKind` | Status |
|---|---|
| `Validation` | 400 (`errors` per field when the error carries them) |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Forbidden` | 403 |
| `TooLarge` | 413 (`Error.TooLarge`, for content a handler refuses after reading it, such as `attachment.too-large`) |
| `UnsupportedType` | 415 (`Error.UnsupportedType`, such as `attachment.unsupported-type`, `attachment.extension-mismatch` and `attachment.content-mismatch`) |
| `Failure` | 422 |

## Paging, sorting and filtering

A list endpoint binds `PagingParameters` (`page`, `pageSize`, `sort`, `filter`) and calls `ToListRequest()`, which parses the three parts and returns a `Result<ListRequest>`; a malformed query answers 400 before the handler runs. The handler resolves the terms against its own allow-lists (`SortableFields<T>`, `FilterableFields<T>`), applies them with `ApplyFilter`/`ApplySort` and materialises with `ToPagedResultAsync`, then maps the page to responses with `ToResponse`.

Wire grammar:

| Part | Grammar | Rules |
|---|---|---|
| Page | `page`, `pageSize` | `page` from 1 and at most `int.MaxValue / pageSize + 1` so the offset cannot overflow, `pageSize` 1 to 200 (default 50); outside → `query.invalid-page` |
| Sort | `field[:asc\|desc]`, terms separated by `,` | at most 5 terms, no repeats, field names are letters and digits starting with a letter; malformed → `query.invalid-sort` |
| Filter | `field:operator:value`, terms separated by `;`, combined with AND | operators `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `contains`, `in`; `in` takes values separated by `\|` and trims the spaces around them; at most 10 terms and 100 values; malformed → `query.invalid-filter` |

Values are percent-encoded, so a value may itself contain `;`, `:` or `|`. A field outside the allow-list answers `query.invalid-field`, an operator the field does not allow `query.invalid-operator`, and a value that does not parse into the field's type `query.invalid-value` — each one naming what was allowed. The default operator set follows the field type (text allows `contains`, booleans only `eq`/`ne`, ids and enums only equality, everything else the comparisons). Filter values are parameterised, never interpolated into SQL, and `ApplySort` always appends the caller's tie-breaker so paging is deterministic.

## Idempotency

A POST endpoint marked `.RequireIdempotencyKey()` demands an `Idempotency-Key` header carrying a UUID; every route that creates something is marked, except the attachments upload and `download-links` (ADR-0022: the middleware would buffer a whole upload and store a link's token in plain text). `IdempotencyMiddleware` (after the feature gate, before the endpoint) fingerprints the request with SHA-256 over method, path, query, actor id and body, and claims the key in `platform_idempotency.IdempotencyKeys` keyed by (actor, key):

| Situation | Answer |
|---|---|
| No header | 400 `idempotency.key-missing` |
| Header that is not a single UUID (quotes allowed) | 400 `idempotency.key-invalid` |
| Key free | the endpoint runs; its status, content type, `Location` and body are stored |
| Key in progress | 409 `idempotency.in-progress` |
| Key used with a different fingerprint | 422 `idempotency.key-reused` |
| Key completed | the stored response is replayed with `Idempotency-Replayed: true` |
| Key completed but the response was larger than `Erp:Platform:Idempotency:MaxStoredResponseBytes` | 409 `idempotency.replay-unavailable` |

A 5xx answer or an exception releases the key so the client may retry with it — unless the unit of work already committed (`UnitOfWorkSignal`), in which case the claim stands and a retry answers 409 `idempotency.in-progress` until it expires, because a committed command must not run twice. Keys expire after `Erp:Platform:Idempotency:RetentionPeriod` and `IdempotencyCleanupService` deletes expired rows every `Erp:Platform:Idempotency:CleanupInterval`. Until the authentication phase the actor of an anonymous request is `ActorIds.Anonymous`, so keys of different callers share one key space; from that phase each Entra identity has its own.
