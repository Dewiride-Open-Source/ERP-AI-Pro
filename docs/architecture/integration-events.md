# Integration events

How one module tells the others what happened, end to end: the event types, publishing into the module's own outbox, the relay in the job runner's poll, the deliveries, the processed records, retention and the rules every publisher and subscriber follows. The decision, its reasons and the rejected alternatives are in [ADR-0060](../adr/0060-integration-events-per-module-outboxes-deliveries-as-job-runs-and-no-order.md); deliveries are runs of [background jobs](background-jobs.md); the setting is in the [configuration register](../configuration.md), the signals in [observability](../guides/observability.md), section "Integration events".

In one paragraph: a module declares an event in its Contracts, a public sealed record with a stable name that carries identifiers, flags, counts, dates and states only. A domain-event handler in the module's `Integration/Publishers` publishes it inside the command's unit of work, which writes one row to the module's own `OutboxMessages` table in the same transaction, so the event exists exactly when the change committed. After the commit the publishing process announces it to the outbox relay, and the API's job runner passes it on at once: one run per subscriber, under the key `event:<event id>`. A subscriber is an `IIntegrationEventHandler<TEvent>` in the consuming module's `Integration/Consumers`; its delivery runs as the system actor in the consuming module's own transaction, which also inserts a processed record, so a delivery that runs again changes nothing. Deliveries happen at least once and in no particular order; a failing delivery is retried and dead-lettered on the jobs page like any job, and the migrator deletes passed-on events after 400 days.

## Components

| Piece | Location | Role |
|---|---|---|
| Event contract | `BuildingBlocks/Kernel/Dewiride.Erp.BuildingBlocks.Kernel/Events/IIntegrationEvent.cs` | `IIntegrationEvent { static abstract string Name { get; } }`, the one member every event implements; Contracts may reference only the Kernel, so the contract lives there |
| Building block | `backend/BuildingBlocks/IntegrationEvents/Dewiride.Erp.BuildingBlocks.IntegrationEvents` (references `BuildingBlocks.Application`, `BuildingBlocks.Kernel`, `BuildingBlocks.Persistence` and `BuildingBlocks.Jobs`) | the publisher, the subscription contract, the types of the two tables, the relay and the retention sweep; it owns no context and no schema |
| Tables | `<module schema>.OutboxMessages` of each publishing module, `<module schema>.ProcessedMessages` of each subscribing module, each created by its module's own migration | the events waiting to be passed on and kept after; the deliveries each subscriber has applied |
| Deliveries | `BuildingBlocks.Jobs`: the runner's poll and one run per event and subscriber | retries, dead letters, Run again and Cancel on `/platform/jobs`, metrics, a span per attempt and the retention of finished runs |
| Extension points | `UnitOfWorkSignal.AfterCommit` (`BuildingBlocks.Persistence/UnitOfWork`), `IJobRunnerSignal` (`BuildingBlocks.Jobs/Queue`), `IJobRunSource` and `AddJobRunSource<TSource>()` (`BuildingBlocks.Jobs/Running`, `Definitions`) | a wake after a commit and a source of runs at every poll |

Inside the building block:

| Folder | Types |
|---|---|
| (root) | `IntegrationEventsRegistration` (`AddErpIntegrationEvents`), `IntegrationEventsOptions` (`Erp:Platform:IntegrationEvents`) |
| `Events` | `IntegrationEventId` (a `readonly record struct`, `IStronglyTypedId`, version 7), `IntegrationEventNames` (the name rule), `IntegrationEventSerializer` (the strict JSON), `IntegrationEventErrors` (`integration-event.unreadable`, `integration-event.too-old`) |
| `Publishing` | `IIntegrationEventPublisher`, `OutboxPublisher`, `IntegrationEventChain` (the depth of a chain of deliveries) |
| `Subscribing` | `IIntegrationEventHandler<in TEvent>`, `IntegrationEventContext`, `IntegrationEventServiceCollectionExtensions` (`AddIntegrationEventHandler`), `IntegrationEventSubscription`, `IntegrationEventCatalog`, `IntegrationEventDelivery` (a delivery's job payload), `IntegrationEventDeliveryJob<TEvent, THandler>` |
| `Persistence` | `OutboxMessage` and `ProcessedMessage` (public, because module contexts declare their sets), with `OutboxMessageConfiguration` and `ProcessedMessageConfiguration`, the configurations their `[EntityTypeConfiguration]` names; `IntegrationEventTables`, the catalogue contexts that map each table, read once from their models |
| `Relay` | `OutboxRelay`, the one `IJobRunSource` (`Announce`, `KeyOf`, `BatchSize` 100, `FullPassInterval` one hour) |
| `Retention` | `IntegrationEventRetentionSweep`, the one `IRetentionSweep` |
| `Telemetry` | `IntegrationEventScope`, a delivery's log scope |

The BuildingBlocks unit and integration tests, the architecture tests and the Host API integration tests see the internals through `InternalsVisibleTo`.

`ErpHostComposition.AddErpPlatform` calls `builder.AddErpIntegrationEvents()` for every host (the API, the test database host, the migrator, `dotnet ef`), right after `AddErpJobs()`. It binds `IntegrationEventsOptions` with `ValidateDataAnnotations().ValidateOnStart()`, in every host because the migrator's retention reads it, and registers `IntegrationEventCatalog` and `IntegrationEventTables` (singletons), `IntegrationEventChain` and `IIntegrationEventPublisher` (`OutboxPublisher`) (scoped), the relay with `AddJobRunSource<OutboxRelay>()` and the sweep with `AddRetentionSweep<IntegrationEventRetentionSweep>()`. It registers no context, so the catalogue, and the migrator's order, are those of [persistence](persistence.md). Only the API runs the runner that asks the relay; the migrator and the test database host compose the relay without ever polling it.

## Declaring an event

An event is a public sealed record in a Contracts assembly, in a namespace ending `.Events` (`Contracts/<Feature>/Events/`), and implements `IIntegrationEvent`:

```csharp
namespace Dewiride.Erp.Modules.Finance.Sales.Contracts.Invoices.Events;

public sealed record InvoiceIssued(Guid InvoiceId, Guid ClientId, DateOnly IssuedOn) : IIntegrationEvent
{
    public static string Name => "finance.sales.invoice-issued";
}
```

- **The name** is its identity in the outbox, in every delivery and on the jobs page, so it never follows the class name: three lower-case kebab-case segments of at most 128 characters (`IntegrationEventNames`), unique, the first two the permission prefix of the module whose Contracts declare it, whichever module publishes it, and never a version (`v<n>`). It names the event's meaning.
- **The members** are `Guid`, `bool`, `int`, `DateOnly`, `DateTimeOffset`, enums declared in a Contracts assembly or in the Kernel, their nullable forms and `IReadOnlyList<T>` of them. An event carries identifiers, flags, counts, dates and states; the subscriber reads anything else, a name or an amount included, through the publisher's Contracts when it runs. So no string travels: no name, email or free text sits in a delivery's payload or in an outbox row kept 400 days. No `long`, `decimal` or `Money` either: the `json` column can round a large or precise number when it stores the text.
- **No id and no time on the type.** The event's `IntegrationEventId` and the time it was published belong to its outbox row, and a subscriber receives them in its `IntegrationEventContext`.
- `IIntegrationEvent` has a `static abstract` member, so the compiler refuses an event without a name, code reads `TEvent.Name` through a type parameter constrained to the interface, and `IIntegrationEvent` itself is never a type argument (CS8920).

`IntegrationEventTests` (`Tests/Architecture`) holds every type that implements `IIntegrationEvent` to the rules of this section: a public sealed record declared in a module's Contracts, in a namespace ending `.Events`, named well, uniquely and under that module's prefix, without a version (`v2`, `…-v2`), and carrying only the members above. The members are read from the serializer's own contract (`IntegrationEventSerializer.Options`), not from the type's public properties, so a non-public member the serializer writes (`[JsonInclude]`), a converter of a member's or of the type's own (`[JsonConverter]`) and a member that collects unknown JSON (`[JsonExtensionData]`) are refused too. No product module publishes yet, so each rule also runs over probe types (`Rules/Probes`), accepting the one that keeps it and refusing each that breaks it.

## Publishing

A module publishes from a domain-event handler in its `Integration/Publishers` folder (`<Module>.Integration.Publishers`), an `IDomainEventHandler<TDomainEvent>` that `AddHandlersFromAssembly` registers like every other handler of the module ([application pipeline](application-pipeline.md)):

```csharp
internal sealed class InvoiceIssuedPublisher(SalesDbContext context, IIntegrationEventPublisher events) : IDomainEventHandler<InvoiceIssued>
{
    public Task HandleAsync(InvoiceIssued domainEvent, CancellationToken cancellationToken)
    {
        events.Publish(context, new Contracts.Invoices.Events.InvoiceIssued(domainEvent.InvoiceId.Value, domainEvent.ClientId, domainEvent.IssuedOn));

        return Task.CompletedTask;
    }
}
```

Here the domain event and the integration event share a name, so the publisher names the Contracts type by its namespace below the module's.

`IIntegrationEventPublisher.Publish<TEvent>(ModuleDbContext context, TEvent integrationEvent)` (`where TEvent : class, IIntegrationEvent`) answers the event's `IntegrationEventId`. `OutboxPublisher`, scoped:

1. names the event with `IntegrationEventNames.Of<TEvent>()`, which throws `InvalidOperationException` for a name that is not well formed;
2. throws `InvalidOperationException` for a context without an open transaction, so a publication outside a unit of work fails, and for a context whose model has no `OutboxMessage`, each message saying what to do;
3. writes the event with `IntegrationEventSerializer` as a delivery carries it: written, read back and written again, which escapes characters the first writing leaves bare, such as the plus of a time zone offset (five bytes more for each), and throws `ArgumentException` above 15 KiB (`MaxBytes`) in that form, so the delivery around it, at most a kilobyte, fits the 16 KiB of a job payload;
4. counts the event's depth with the scoped `IntegrationEventChain`: 0 outside a delivery, one more than the delivered event's inside one, and `InvalidOperationException` past `IntegrationEventChain.MaxDepth` (section "Chains of events");
5. adds an `OutboxMessage` to the context: `IntegrationEventId.Create()`, the name, the JSON, `TimeProvider.GetUtcNow()` as `OccurredAt`, `IRequestOrigin.CorrelationId` (a request's correlation id, or the trace id of a job attempt that publishes in turn; at most 64 characters) and the depth;
6. registers `UnitOfWorkSignal.AfterCommit` with the relay's `Announce`, at every publication: after the commit the relay is due and the local runner is woken through `IJobRunnerSignal`.

The domain events of a command are dispatched inside its transaction, round by round, and each round's `SaveChangesAsync` saves what the round before added, so the row is saved in the transaction of the change that raised the domain event: a unit of work that fails, throws or rolls back leaves no row, and a committed unit exactly one per publication. Once the outermost unit has committed, `EfUnitOfWork` runs the callbacks, each in its own `try`/`catch` that logs a failure at `Error` and goes on, so no callback can turn the committed command into a failure, and the local runner polls at once. A publisher never calls `SaveChangesAsync`, as no handler does, and every type whose constructor takes an `IIntegrationEventPublisher` lives in a namespace ending `.Integration.Publishers` (`IntegrationEventTests`).

## Subscribing

A subscriber is a sealed `IIntegrationEventHandler<in TEvent>` in the consuming module's `Integration/Consumers` folder (`<Module>.Integration.Consumers`), with the `JobDefinition` its deliveries run under:

```csharp
internal sealed class ClientDraftsHolder(ICommandHandler<HoldClientDraftsCommand, int> holdDrafts) : IIntegrationEventHandler<ClientDeactivated>
{
    public static JobDefinition Definition { get; } = new(
        "finance.sales.client-drafts-hold",
        "Hold a deactivated client's drafts",
        "Holds the draft invoices of a client who was deactivated, so none of them is issued.",
        JobRetryPolicy.Default,
        TimeSpan.FromMinutes(10));

    public async Task<Result> HandleAsync(ClientDeactivated integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        var held = await holdDrafts.HandleAsync(new HoldClientDraftsCommand(integrationEvent.ClientId), cancellationToken).ConfigureAwait(false);
        if (held.IsFailure)
        {
            return held.Error!;
        }

        return Result.Success();
    }
}
```

The module registers it from its `AddServices`:

```csharp
builder.Services.AddIntegrationEventHandler<ClientDeactivated, ClientDraftsHolder>(ClientDraftsHolder.Definition);
```

- **The subscriber is a job.** `AddIntegrationEventHandler<TEvent, THandler>(JobDefinition definition)` names the event with `IntegrationEventNames.Of<TEvent>()`, registers the delivery job `IntegrationEventDeliveryJob<TEvent, THandler>` under `definition` with `AddJob`, which refuses a duplicate name or job type, and adds the subscription the relay reads (`IntegrationEventSubscription`: the event's name, the subscriber's name, the handler's type and its enqueue), which `IntegrationEventCatalog` holds by event and by handler, refusing a handler registered twice. The subscriber's name is `definition.Name`, under the consuming module's permission prefix (`JobTests` reads a delivery job's module from its handler), and its title and one-sentence description are what the jobs page shows for its deliveries; the retry policy and the attempt timeout are its deliveries' ([background jobs](background-jobs.md), section "Defining a job").
- **It is created for each delivery**, with `ActivatorUtilities.CreateInstance` in the run's scope, and disposed after it, never registered, as a job is; so its constructor may take any service the API composes, and `JobTests` builds it in a scope of the API host, as it builds every job, so a constructor that asks for anything else fails the build.
- **It is named for what it does with the event** (`ClientDraftsHolder`, `finance.sales.client-drafts-hold`), not for the event, because one event has several subscribers ([naming](naming-and-namespaces.md)).
- **It translates the event into its module's own command** and calls that command's handler, the pipeline, so the command's validation and rules apply and its unit of work joins the delivery's transaction ([application pipeline](application-pipeline.md), section "Unit of work"). It never calls `SaveChangesAsync`.
- **It returns a `Result`**, whose kind decides the retry as for any job: `Unavailable` and `Conflict` are retried, every other failure dead-letters at once.
- **Its module owns one context, which maps `ProcessedMessage`** (section "The tables"), because the delivery inserts the processed record there; `IntegrationEventTests` checks it for every subscriber the API host registers, and that every subscriber is a sealed class of a namespace ending `.Integration.Consumers`.
- **`IntegrationEventContext(IntegrationEventId EventId, DateTimeOffset OccurredAt, JobContext Job)`** gives the event's id, the time it was published and the run's id and attempt.

What a delivery does, after the runner claimed its run and opened the attempt as for any job (a scope of its own, the system as actor, a span and a lease):

1. `JobPayloads` reads the run's payload, `IntegrationEventDelivery(Guid EventId, string EventName, string Source, DateTimeOffset OccurredAt, string? CorrelationId, int Depth, JsonElement Event)`: the event itself, its name, the schema of the outbox that held it, when it was published, the publishing scope's correlation id and how many deliveries led to it. A delivery therefore never reads the outbox, and a re-run works after the row was deleted.
2. The delivery opens the log scope `IntegrationEventScope` inside the attempt's (`IntegrationEventId`, `IntegrationEventName`, `IntegrationEventSource` and, when the publication had one, `PublishedCorrelationId`).
3. It refuses, logged at `Error` and dead-lettered at once, a delivery that names another event than the subscriber's (`integration-event.unreadable`), which a job name reused for another event's subscriber would bring, and one whose event occurred `Erp:Platform:Jobs:FinishedRunRetention` or longer ago (`integration-event.too-old`, a `Failure`), because the processed record of an earlier handling is kept only that long (section "Retention").
4. `IntegrationEventSerializer` reads `Event` as `TEvent`, strictly (section "Reading and changing an event"); an event it cannot read is logged at `Error` and fails with `integration-event.unreadable`, a `Failure`, which dead-letters at once.
5. It finds the context the subscriber's assembly owns, `DbContextCatalog.OwnedBy(typeof(THandler).Assembly)`, which throws when the assembly owns two catalogue contexts; an assembly that owns none throws too, because the subscriber would have nowhere to record what it handled.
6. It hands the delivery to the scoped `IntegrationEventChain`, so an event published in its unit of work counts one delivery more, creates the subscriber and runs a unit of work (`EfUnitOfWork.RunAsync`) on that context. When a `ProcessedMessage` with the event's id and the subscriber's name exists, the event was handled: the delivery logs that it changed nothing, at `Information`, and succeeds without running the subscriber. Otherwise it runs `HandleAsync` with `IntegrationEventContext(EventId, OccurredAt, Job)` and, on a success, adds the `ProcessedMessage`, and the unit commits the subscriber's changes, their change records and the processed record together, or none of them; a failed `Result` rolls everything back and is the attempt's outcome.
7. The runner records the outcome as for any job.

**Work after one's own commit.** A module that wants work done only once its own change has committed publishes an event and subscribes to it itself: the delivery's run is created only after the commit, so a unit of work that failed, threw or rolled back runs nothing ([background jobs](background-jobs.md), section "Enqueuing").

## The tables

| Table | Type | Columns | Keys and indexes |
|---|---|---|---|
| `OutboxMessages` | `OutboxMessage`, neither `IAuditable` nor `IAppendOnly` | `Id` (`IntegrationEventId`, version 7, set in code), `EventName varchar(128)`, `Payload json` (the event), `OccurredAt` (when it was published), `CorrelationId varchar(64) NULL`, `Depth int` (how many deliveries led to it), `RelayedAt NULL` (when the relay passed it on) | primary key `Id`; `(OccurredAt, Id)` where `RelayedAt IS NULL`, for the relay; `RelayedAt` where it is set, for retention |
| `ProcessedMessages` | `ProcessedMessage`, `IAppendOnly` | `EventId`, `Subscriber varchar(128)` (the subscriber's job name), `ProcessedAt` | primary key `(EventId, Subscriber)`; `ProcessedAt`, for retention |

Every time is a UTC `datetimeoffset`. Each type carries `[EntityTypeConfiguration]`, so EF Core applies the building block's configuration in every context whose model includes the type and in no other; the configuration names the table and no schema, so the table lands in the context's default schema, the module's. `IntegrationEventId` is found by the strongly-typed id convention, because the module references the building block ([persistence](persistence.md), section "Conventions").

A module opts in from its context:

```csharp
public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
```

The first set for a module that publishes, the second for one that subscribes, both for one that does both; then a migration of its own, `node scripts/ef/ef.ts add <Name> --context <key>` ([migrations guide](../guides/migrations.md)). No product context maps either table yet.

- `OutboxMessage` is neither `IAuditable` nor `IAppendOnly`, the one such type a module context maps: the relay marks a row with `ExecuteUpdate`, which the bulk guard refuses on both, and a change record for every mark would fill the books' trail with the ERP's bookkeeping, as a job run would. Its rows have no change records.
- `ProcessedMessage` is inserted once, in the delivery's transaction, and deleted only by retention.
- A catalogue context maps, besides its own types, only `ChangeRecord`, `OutboxMessage` and `ProcessedMessage` (`PersistenceTests`; `ChangeRecordTests` lets `OutboxMessage` alone be neither auditable nor append-only, both exceptions proven on probe contexts), and a context that maps either table maps it as the configuration does (`IntegrationEventTests`): in its own schema under the configured table name, the outbox's `Payload` a `json` column with its two filtered indexes, the processed records keyed by `(EventId, Subscriber)`. A change to that configuration needs a migration in every module that maps the table.

## The relay

`OutboxRelay` is one singleton `IJobRunSource`, registered with `AddJobRunSource<OutboxRelay>()`, so the API's runner asks it at every poll, after the scheduler and before the lease recovery and the claims, and claims what it enqueued in the same poll ([background jobs](background-jobs.md), section "Claims, leases and one attempt"). `IntegrationEventTables` finds once, from their models, the catalogue contexts that map `OutboxMessage`.

**When it reads.** A read of a module's outbox writes a command line of that context at `Information`, so the relay does not read every outbox at every poll, only when a pass is due:

- after a commit of its own process published an event (`Announce`, which the publisher registers to run after the commit and which also wakes the runner);
- at its first poll after the API starts;
- at the poll after a pass in which an event or an outbox failed for the first time;
- and once `FullPassInterval` (one hour) has gone by since its last pass, which also finds an event whose process stopped between its commit and its announcement, or one another process committed, and tries again what keeps failing.

**A pass.** For each context that maps the outbox, in a scope and a `try`/`catch` of its own:

1. it reads the rows whose `RelayedAt` is null, in pages of 100 (`BatchSize`) ordered by `(OccurredAt, Id)`, until a page holds fewer: a version 7 id does not sort by time in SQL Server, which compares the last six bytes of a `uniqueidentifier` first;
2. it passes each row on in its own `try`/`catch`: for each subscriber of its `EventName` (`IntegrationEventCatalog.SubscribersOf`) it enqueues that subscriber's delivery job with the row's `IntegrationEventDelivery` under the key `event:<the event id in D format>` (`OutboxRelay.KeyOf`), then marks the row with one `ExecuteUpdate` that sets `RelayedAt` where `Id` is the row's and `RelayedAt IS NULL`, and logs it passed on at `Information`;
3. it marks an event without subscribers without enqueuing anything;
4. a row it cannot pass on is logged at `Error` with its exception, the first time only, and left out of the rest of the pass, so it never holds back the events behind it. Its first failure makes the next poll's pass due, for a failure that heals at once; after that it waits for the hourly pass, or a pass a commit makes due, so an event that keeps failing costs one read an hour. Once it passes, the recovery is logged at `Information` with the number of failed passes.

An outbox whose read fails, such as one whose table a developer's database has not got yet, is handled the same way: logged at `Error` with its exception, the first time only, passed over so the outboxes after it are still read, tried again at the next poll once and then hourly, and its first read after failures logged at `Information`. The relay runs on the runner's loop, outside any request, so each delivery run's `RequestedBy` and `Enqueued` event name the system. Anything else that fails a pass is the runner's to log, on the source's own streak ([observability](../guides/observability.md), section "Background jobs").

**Latency.** The publishing process announces the event after its commit and wakes its runner, which polls and passes it on at once; a wake that comes while the runner polls makes it poll once more. An event whose process stopped between the commit and the announcement is passed on at the first poll after the API starts again, and an event committed by another process (the migrator, a second instance) within `FullPassInterval`. A delivery then waits for one of the runner's `MaxConcurrentRuns` slots, which it shares with every other job.

**Twice is harmless.** Two relays reading one row at once, or a relay that enqueued and failed before its mark, enqueue under the same key, and `IJobQueue` answers the run that key already names; the mark changes the row for one of them only.

**The owner's SQL Server.** With `READ_COMMITTED_SNAPSHOT` off, the default of SQL Server, the relay's read of a module's outbox waits behind a transaction that added a row and has not committed, at most the command timeout, after which the relay logs the outbox's failure and reads it again at the next poll, once, then hourly or after the next announcement. Azure SQL Database, where it is on, reads the last committed rows and never waits.

## Deliveries are job runs

Each delivery is a run of its subscriber's job, so everything in [background jobs](background-jobs.md) applies:

- **The jobs page** lists each delivery under its subscriber's title and description, with the key `event:<event id>`, so an administrator finds the deliveries of one event by filtering on the key. The payload is never shown.
- **Retries and the dead letter** follow the subscriber's `JobRetryPolicy`: a `Conflict` or `Unavailable` result, an exception, the attempt timeout or an expired lease are retried after doubling delays, and any other failure, an unreadable event (`integration-event.unreadable`), an event too old (`integration-event.too-old`) or an unreadable payload (`job.payload-unreadable`) included, dead-letters at once.
- **Run again** returns a failed, dead-lettered or cancelled delivery to pending; when its change had committed, the processed record makes the new attempt change nothing, and once its event is `FinishedRunRetention` old the attempt dead-letters as too old instead of running. A succeeded delivery is not run again (409 `job.run-not-rerunnable`). **Cancel** ends a delivery that will not be needed.
- **Isolation.** Every delivery has its own run and its own transaction, so a subscriber that fails holds back neither another subscriber's delivery of the same event nor another event.
- **Keys.** `(JobName, IdempotencyKey)` is unique, so an event is delivered to a subscriber under one run, for as long as that run is kept (`Erp:Platform:Jobs:FinishedRunRetention`, 400 days).
- **Feature flags** gate routes and pages only: a module's publishers publish and its subscribers receive their deliveries while its flag is off, as its jobs run.
- **A subscriber added in a release** receives only the events relayed after that release starts; nothing replays earlier ones. **A subscriber removed in a release** leaves its unfinished runs pending, under a job the release does not define, until an administrator cancels them.

## At least once and idempotency

| Situation | What keeps the change applied once |
|---|---|
| two relays pass one event on at once, or a relay fails between its enqueue and its mark | the job key: the second enqueue answers the existing run |
| two runners execute one delivery (a process paused past its lease while another recovered the run) | the processed record's primary key: the second insert fails, its unit of work rolls back with `concurrency.conflict`, which is retried, and the retry finds the record and succeeds |
| an administrator runs a delivery again whose run was recorded failed, dead-lettered or cancelled although its change had committed | the processed record: the delivery succeeds without running the subscriber |
| the publishing command is repeated after its commit's acknowledgement was lost (the execution strategy retries as if it had rolled back) | nothing: the second event has a new id, so the subscriber's change must also hold against a second event with the same meaning, by its business key |
| the subscriber's effect lies outside the database (Microsoft Graph, mail) and the delivery's transaction fails after it | nothing: the subscriber makes that effect idempotent itself, for instance through a job enqueued under a key built from the event's id |

So every subscriber is idempotent, as every job is.

## Chains of events

An event published in a delivery's unit of work counts one delivery more than the event delivered: `OutboxMessage.Depth` and `IntegrationEventDelivery.Depth` are 0 for an event a request or any other job published, and the delivery job hands the publisher the delivered event's through the scoped `IntegrationEventChain`. Past `IntegrationEventChain.MaxDepth` (10) the publication throws `InvalidOperationException` naming both events, so the delivery's unit of work saves nothing, the attempt fails as an exception, is retried and dead-letters on the jobs page. Subscribers whose work leads back to an event they handle, such as a module's own subscriber whose command raises the domain event its publisher turns into the same event, would otherwise publish without end, each hop passed on at once, as `EfUnitOfWork` stops a cycle of domain events after `MaxDispatchRounds`. A subscriber that publishes from a scope of its own, or through a job it enqueues, starts a new count, so it does neither.

## No order

No order is promised, between events or between the subscribers of one event. The relay reads by `(OccurredAt, Id)`, but each delivery is a run of its own: runs are claimed `MaxConcurrentRuns` at a time, a failed one waits for its retry while later ones run, and an event committed before another can be relayed after it. A subscriber therefore reads the current state of what it works on when it runs, as a job does, and never relies on having seen an earlier event.

## Reading and changing an event

`IntegrationEventSerializer` writes and reads events with the rules of `JobPayloads` (System.Text.Json with the Web defaults, strict numbers, enums as camelCase names and never numbers) plus `RespectRequiredConstructorParameters` and `RespectNullableAnnotations`:

| The stored JSON | Read as |
|---|---|
| lacks a member that has no default | unreadable |
| lacks a member whose constructor parameter has a default | the default |
| holds `null` for a member that cannot be null, or a value of the wrong type | unreadable |
| holds a member the type does not know | read, the member skipped |
| holds an enum name the type does not know | unreadable |

Unreadable fails the delivery with `integration-event.unreadable` and dead-letters it. A member the type does not know is skipped, so an event published by a release is still read by the release before it after a rollback.

The rules of change follow:

- **A name never changes.**
- **A member is added only as a constructor parameter with a default**, so the events published before it, and still queued, read with the default.
- **Any other change** (a member removed, renamed or retyped, a meaning changed) **is a new event** with a name for its meaning, never a version. The old event is retired in two releases, because a row of it still waiting once its subscribers are gone would be marked without a delivery, and the runner claims no run of a job its release does not define: the first release stops publishing it and publishes the new one, and a later release deletes the old event and its subscribers whole once no outbox holds a row of it waiting and no delivery run of its subscribers is unfinished on the jobs page. The subscribers the first release keeps are no compatibility path (root `CLAUDE.md` §8): nothing new reaches them, and they exist only to finish the events and runs already written.
- **An enum an event carries** is part of the event: an enum name the release before does not know is unreadable to it, so an event carrying a member added in a release dead-letters after a rollback to the release before.
- **`IntegrationEventDelivery` is part of every queued run**: a change to it needs new subscriber job names, because the runs already queued carry its old shape, which `JobPayloads` would refuse as `job.payload-unreadable`.

## Who a delivery runs as

A delivery is an attempt of a background job, so its scope comes from the runner, outside any request: `IActorContext` gives `ActorIds.System`, and `IRequestOrigin` gives the attempt's trace id as the correlation id, which the run's `Started` event keeps as `TraceId` ([background jobs](background-jobs.md), section "Who a job runs as"). A subscriber's change records therefore name the ERP (`actorKind` `system` on the audit page) and carry that trace id.

- **From a change back to its cause.** The trace id leads to the delivery run, whose key names the event; the event's outbox row keeps the publishing scope's correlation id, under which the publisher's change records name the person who acted. The run and the row are kept 400 days, as the server's journal keeps its lines, while change records are kept 8 financial years, so after 400 days a subscriber's records still say that the ERP made the change but no longer lead to the person whose change caused it.
- **Authorisation belongs to the publisher.** `ICallerPermissions.Holds` throws outside a request ("The caller's permissions are read only during a request."), so a subscriber checks nothing of a person: it acts as the system on what the event says. The module that publishes checks, before it publishes, every permission the subscribers' effects need of the person who acts; an event that asks the users module to invite a person with ERP roles needs the asker's `identity.users.people.assign-roles`.

## Retention

`IntegrationEventRetentionSweep`, an `IRetentionSweep`, runs in the migrator alone: after the seeders of `migrate`, and on `retain`, which `erp-ai-pro-retain.timer` runs every night at 01:00 India time ([migrations guide](../guides/migrations.md), section "Retention"). For every catalogue context that maps either table it deletes:

- the outbox rows whose `RelayedAt` is older than `Erp:Platform:IntegrationEvents:PublishedEventRetention` (400 days, the owner's choice of 2026-11-02), oldest first by the filtered `RelayedAt` index; a row not yet relayed is never deleted, whatever its age;
- the processed records whose `ProcessedAt` is older than `Erp:Platform:Jobs:FinishedRunRetention` (400 days), oldest first; a delivery whose event occurred that long ago is refused before its subscriber runs (section "Subscribing"), and a record is never older than its event, so no delivery that runs can miss the record of an earlier handling;

both in statements of 100 rows (`IntegrationEventRetentionSweep.BatchSize`), each committed on its own, as `JobRunRetentionSweep` deletes runs, and logs at `Information`, per schema, how many it deleted and before when. A failed sweep is logged at `Error` and the next run deletes what it left; `retain` exits 1. The API deletes nothing in these tables.

## Observability

- **Logs**, naming an event by its name, its id and the schema of its outbox, never its JSON. The relay logs each event it passed on, with the number of subscribers, at `Information`; an event it cannot pass on, or an outbox it cannot read, at `Error` with the exception, the first time only; and each recovery at `Information` with the number of failed passes. The runner logs anything else that fails a pass on the source's own streak, and a delivery refused for a chain past `MaxDepth` as the attempt's exception. A delivery logs inside the attempt's scope (`JobRunId`, `JobName` the subscriber's name, `Attempt`, `CorrelationId` the attempt's trace id) and its own `IntegrationEventScope` (`IntegrationEventId`, `IntegrationEventName`, `IntegrationEventSource`, `PublishedCorrelationId`, the correlation id of the request whose change published the event), so a subscriber's lines lead back to that request in the journal; it logs an unreadable event, a delivery of another event than its subscriber's and an event too old to handle at `Error`, and a delivery its processed record already covered at `Information`.
- **Metrics.** No meter of its own: `erp.jobs.enqueued` counts each relay enqueue by subscriber (`existing` when the key already named a run, as when a relay passes an event on twice), and `erp.jobs.attempts` and `erp.jobs.attempt.duration` count and time the deliveries by subscriber and outcome.
- **Traces.** Each delivery is a span `job <subscriber name>` of the activity source `Dewiride.Erp.Jobs`, parent of the subscriber's handler and SQL spans.
- **Health.** Readiness gains no check: the building block owns no context, and the relay's failures reach only the log, because nothing alerts anyone ([ADR-0055](../adr/0055-the-production-host-restarts-an-unhealthy-container-and-alerts-nobody.md)).

The details are in [observability](../guides/observability.md), section "Integration events".

## Configuration

| Key | Default | Rule | Read by | Takes effect |
|---|---|---|---|---|
| `Erp:Platform:IntegrationEvents:PublishedEventRetention` | `400.00:00:00` | 1 to 3650 days; never deletes an event not yet passed on | the migrator (`IntegrationEventsOptions`, validated on start in every host) | at the migrator's next run |

It is seeded unlabelled in `infra/appconfig/defaults.json` and registered in the [configuration register](../configuration.md). The deliveries follow the jobs' settings (`Erp:Platform:Jobs:*`), `FinishedRunRetention` also bounding the age of an event a delivery still handles, read by the API at its start, and the integration events need no secret and no Key Vault reference.

## Testing

- **No loop in a test host, so nothing is relayed by default.** The relay is a run source of the job runner, and `ErpApiFactory` removes the hosted `JobRunnerService` unless a test calls `WithJobRunner()` ([testing guide](../guides/testing.md), section "Rules"). Every host of a test process shares one test database, so a test relays and runs the events it published itself, by their ids; the relay's own tests of its passes are the exception, because a pass passes on whatever the collection's earlier tests left waiting.
- **The building block's own tests** relay one event by its id through the internal `OutboxRelay.RelayOneAsync(Type contextType, IntegrationEventId id, CancellationToken cancellationToken)` and then run each delivery run by its id, against SQL Server; `RelayTests` also drive the relay's passes through `OutboxRelay.EnqueueDueAsync`, and `OutboxReadFailure`, an interceptor both hosts carry, fails every read of the sample outbox while a test has it on. The fixture `IntegrationEventsDatabase` builds two hosts of one installation over the test database, as `JobsDatabase` does, each composing the persistence sample's `SampleDbContext` (schema `test_sample`, the one context of that test assembly), which maps both tables beside its sample shipments and receipts: `SampleShipment` raises the domain event `SampleShipmentDispatched`, `ShipmentDispatchedPublisher` publishes `test.events.shipment-dispatched` (`ShipmentDispatched`), and the subscribers `ShipmentReceiptRecorder` and `ShipmentLabelPrinter`, of the same context, record receipts, under subscriber names of the fixture's own. The test classes share the collection `IntegrationEventPublishers`, which runs them one at a time, and only `RunnerPollTests` lets a runner poll.
- **A module that publishes or subscribes** keeps its unit tests on hand-rolled fakes, as for jobs: its publisher against a recording `IIntegrationEventPublisher`, its subscriber's `HandleAsync` called with an `IntegrationEventContext`. The testing library has no driver for deliveries yet: the first module that publishes adds one with its tests. The architecture tests check its events, publishers, subscribers and mappings without a test of its own, and `node scripts/ef/ef.ts pending --all` fails until its migration adds the tables.

## Tests

| Project | Covers |
|---|---|
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.UnitTests/IntegrationEvents` | event names (`IntegrationEventNamesTests`: three lower-case kebab segments, the 128-character limit, a malformed name refused naming the event), the serializer (`IntegrationEventSerializerTests`: a round trip at the bounds of an `int`, an optional member absent and an unknown one present read, a missing member, a null where none is allowed or a loose value unreadable, an event over 15 KiB refused, an event measured as a delivery carries it, and the largest event fitting a job payload inside the largest delivery), the chain (`IntegrationEventChainTests`: depth 0 outside a delivery, one more inside one, refused past `MaxDepth` naming both events), the subscriptions (`IntegrationEventCatalogTests`: by event, a handler registered twice, a handler not registered) and the options' range (`IntegrationEventsOptionsTests`) |
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.UnitTests/Persistence` | `UnitOfWorkSignalTests`: each callback run once in order, only those registered since a rollback, a callback that throws logged while the next still runs, a null callback refused; `DbContextCatalogTests`: `OwnedBy` answering an assembly's one context, nothing for an assembly without one, and refusing one that owns two |
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.IntegrationTests/Persistence/UnitOfWork` (SQL Server) | `UnitOfWorkTests`: the after-commit callbacks run once the commit is done and the transaction has ended, dropped when the unit fails or throws, and run only after the outer commit when a unit joins another |
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.IntegrationTests/Jobs` (SQL Server) | `JobRunnerServiceTests`: a run source's run claimed and run, and a failing source's streak while the runner keeps claiming |
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.IntegrationTests/IntegrationEvents` (SQL Server, the fixture `IntegrationEventsDatabase`) | `PublishingTests`: one outbox row at depth 0 when the unit commits, none when it fails or throws after publishing, a publication outside a unit of work and a context without an outbox refused; `RelayTests`: one delivery per subscriber under the event's key and the row marked, an event without subscribers marked without a run, no second delivery after a stop between the enqueue and the mark, one delivery per subscriber from two runners at once, the first pass after a start passing on an event no announcement reached, an event committed in the process after a pass passed on at the next poll, an event another process committed waiting for the hourly full pass instead of being read at every poll, an event that keeps failing logged once, tried again at the next poll once and then hourly while the events behind it go on, an outbox that cannot be read treated the same way, and more events than a page all passed on; `DeliveryTests`: a delivery handled as the system with its processed record, a delivery published by another module recorded in the subscriber's own context, a delivery of an event already handled changing nothing, two runs of one delivery at once applying the change once, a failing subscriber retried and dead-lettered without holding back the other, an event that no longer reads and a delivery of another event dead-lettered as unreadable, an event as old as the processed records are kept dead-lettered as too old while a younger one is handled, and an event a subscriber publishes counting one delivery more, refused with nothing saved past the longest chain; `RunnerPollTests`: a committed event passed on and delivered to every subscriber, by its announcement, without waiting for a poll interval; `RetentionTests`: passed-on events deleted past their retention and a waiting one never, processed records deleted past the runs' retention, and more rows than a statement all deleted; `IntegrationEventTablesTests`: both tables mapped as their configurations say, and the catalogue contexts that declare them |
| `Tests/Architecture` | `IntegrationEventTests`, each rule over the product's types and over probe types (`Rules/Probes`): events (public sealed records of a module's Contracts in an `.Events` namespace, a well-formed, unique name under the module's prefix and without a version, plain members; two events of one name reported), subscribers sealed in `.Integration.Consumers`, publishers (types taking `IIntegrationEventPublisher`) in `.Integration.Publishers`, every subscriber's module context mapping `ProcessedMessage`, every catalogue context's tables mapped as their configurations say (probe contexts in `Rules/Probes/Persistence` that remap a table, drop a filtered index or re-key the processed records refused), and a delivery job's owner being its subscriber; `PersistenceTests` and `ChangeRecordTests` with the two tables' exceptions, each proven on a probe context that maps them and one that maps a type of another assembly; `JobTests` naming a delivery job by its subscriber's module and building its subscriber, each proven on probe delivery jobs |
