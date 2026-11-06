# Background jobs

How the ERP defines, queues, claims, retries, schedules and records the work it does on its own, end to end. The decision, its reasons and the rejected alternatives are in [ADR-0059](../adr/0059-background-jobs-a-persistent-queue-leases-ist-schedules-and-the-nightly-account-check.md); the settings are in the [configuration register](../configuration.md), the routes and pages in the [routing register](../routing.md), the signals in [observability](../guides/observability.md), section "Background jobs".

In one paragraph: a module defines a job, a class with a definition (its name, title, description, retry policy and attempt timeout), and enqueues runs of it under an idempotency key, or gives it a schedule. A run is a row of `platform_jobs.JobRuns`. The runner hosted by the API claims a due run with one conditional update that hands it a lease, runs the job in a scope of its own as the system actor, renews the lease while the job runs and records the outcome with the same kind of update: succeeded; failed and due again after a doubling delay; given up (dead-lettered); interrupted by a shutdown and due again at once; or cancelled. Every step is an event of the run, inserted once and never changed. A recurring job follows a five-field cron expression in India time, and the scheduler enqueues one run per occurrence, only the latest after the API was down. Administrators read the jobs and runs at `/platform/jobs`, run a run again or cancel it, and the migrator deletes finished runs after 400 days. A building block can feed the runner too: at every poll the runner asks its run sources for runs, and the outbox relay of [integration events](integration-events.md) is one, turning each committed event into one run per subscriber.

## Components

| Piece | Location | Role |
|---|---|---|
| Building block | `backend/BuildingBlocks/Jobs/Dewiride.Erp.BuildingBlocks.Jobs` | the contracts a module uses (`IJob`, `IJob<TPayload>`, `JobDefinition`, `JobRetryPolicy`, `IJobQueue`), the extension points another building block uses (`IJobRunnerSignal`, `IJobRunSource`), the queue, the runner, the scheduler, the cron parser, the administration service `IJobAdministration`, the retention sweep and the telemetry |
| Module | `backend/Modules/Platform/Jobs` (`Dewiride.Erp.Modules.Platform.Jobs`, schema `null`) | the five routes under `/api/platform/jobs` and their command and query handlers, thin wrappers over `IJobAdministration` that run no unit of work, because the module owns no context; the permissions in `JobsPermissions` (Contracts) |
| Data | SQL schema `platform_jobs`, `JobsDbContext` (migration `InitialJobs`, `ef.ts` key `jobs`) | the runs, their events and the schedules' state |
| Web | `frontend/apps/web/src/features/platform/jobs`, routes `/platform/jobs` and `/platform/jobs/runs/[runId]` | the jobs with their schedules, the runs, Run again and Cancel |
| The first job | `identity.users.directory-account-check`: `People/Hosting/DirectoryAccountCheckJob` and `AccountCheckOptions` of `Identity/Users` | the nightly account check of [ADR-0047](../adr/0047-user-lifecycle-the-account-check-and-the-team-handover.md) |

Inside the building block:

| Folder | Types | Visibility |
|---|---|---|
| (root) | `JobsRegistration` (`AddErpJobs`, `AddErpJobRunner`), `JobsOptions` | public |
| `Definitions` | `IJob` and `IJob<TPayload>` (one file, `IJob.cs`), `JobContext(RunId, Attempt)`, `JobDefinition`, `JobRetryPolicy`, `JobServiceCollectionExtensions` (`AddJob`, `AddRecurringJob`, `AddJobRunSource`); `JobNames`, `JobRegistration`, `JobCatalog` | the contracts public, the last three internal |
| `Queue` | `IJobQueue`, `IJobRunnerSignal`; `SqlJobQueue`, `JobKeys`, `JobPayloads`, `JobSignal` (the runner's wake, which implements `IJobRunnerSignal`) | the interfaces public, the rest internal |
| `Administration` | `IJobAdministration`, `JobErrors`, `JobDetails`, `JobScheduleDetails`, `JobRunDetails`, `JobRunHistory`, `JobRunEventDetails`, `JobActorDetails`, `JobActorKind`; `JobAdministration`, `JobActorNames` | public, the last two internal |
| `Domain` | `JobRun`, `JobRunId`, `JobRunStatus`, `JobRunEvent`, `JobRunEventId`, `JobRunEventKind`, `JobSchedule`, `StoredError` (an error's code and message as a run stores them) | `JobRunId`, `JobRunStatus` and `JobRunEventKind` public, the rest internal |
| `Running` | `JobRunnerOptions`, `IJobRunSource`; `JobRunner`, `JobRunnerService`, `JobRunnerInstance`, `JobInvoker` | the options and `IJobRunSource` public, the rest internal |
| `Running/Attempts` | `ClaimedRun`, `CompletedAttempt`, `JobAttemptEnd`, `JobAttemptErrors`, `JobAttemptOutcome`, `JobLease`, `LeaseClaim`, `LeaseState` | internal |
| `Scheduling` | `CronSchedule`, `CronField`, `CronFieldKind`, `JobScheduler`, `OccurrenceKeys`, `OccurrenceOutcome` | internal |
| `Persistence` | `JobsDbContext`, `JobRunConfiguration`, `JobRunEventConfiguration`, `JobScheduleConfiguration`, `JobRunTransitions` and `JobScheduleTransitions` (every state change of a run and of a schedule, each a compare-and-set statement), `AttemptEnding`, `Migrations/` | internal |
| `Retention`, `Telemetry` | `JobRunRetentionSweep`; `JobsMetrics`, `JobsTelemetry`, `JobAttemptScope` (an attempt's log scope) | internal |

The BuildingBlocks unit and integration tests, `Dewiride.Erp.Testing`, the architecture tests and the Host API integration tests see the internals through `InternalsVisibleTo`.

`ErpHostComposition.AddErpPlatform` calls `builder.AddErpJobs()` for every host (the API, the test database host, the migrator, `dotnet ef`), after `AddErpAttachments()`. It registers `JobsDbContext` with `AddModuleDbContext<JobsDbContext>("platform_jobs")` (the readiness check `database:platform_jobs`, the catalogue entry after `files`), binds `JobsOptions` from `Erp:Platform:Jobs` with `ValidateDataAnnotations().ValidateOnStart()`, and registers `JobCatalog` (the jobs the modules registered, refusing a duplicate name or type and an invalid retry policy), `IJobQueue` (`SqlJobQueue`), `IJobAdministration` (`JobAdministration`), the singletons `JobSignal`, which is also the host's `IJobRunnerSignal`, and `JobsMetrics` (the queue counts every enqueue) and the retention sweep `JobRunRetentionSweep`. `IJobRunnerSignal.Wake()` lets code outside the building block wake the runner of its process, as the outbox relay does when a commit announces an event; in a host without a runner it wakes nothing. A building block adds a run source with `services.AddJobRunSource<TSource>()`, which registers it once, whatever the number of calls, as a singleton of its own type and as an `IJobRunSource`; every host composes it and only the API's runner asks it (section "Claims, leases and one attempt"). `AddErpIntegrationEvents` adds the outbox relay that way, and its publisher reaches the relay by its type ([integration events](integration-events.md)). Only the API's `Program.cs` calls `builder.AddErpJobRunner()`, last of its registrations so the runner stops first, which binds `JobRunnerOptions` from the same section with `ValidateOnStart()` and registers `JobRunnerInstance` (the name the process writes as `LeaseHolder`), `JobInvoker`, `JobRunner`, `JobScheduler` and the hosted `JobRunnerService`; `LayerTests.JobRunner_IsComposedByTheApiHostAlone` fails when any product assembly, the migrator included, or the testing library calls it (the building block's own integration tests compose a runner in `JobsDatabase`). The migrator and the test database host therefore compose the queue, the administration and the retention, never a runner.

## Data model

Schema `platform_jobs`, migrations history `platform_jobs.__EFMigrationsHistory`:

| Table | Columns | Keys and indexes |
|---|---|---|
| `JobRuns` | `Id` (`JobRunId`, version 7, set in code), `JobName varchar(128)`, `IdempotencyKey varchar(200)`, `Payload json` (null for a job without one), `Status tinyint`, `DueAt` (the earliest start of the next attempt), `CreatedAt`, `RequestedBy` (the actor who enqueued it), `AttemptCount` (attempts over the run's life), `FailureCount` (failures since the enqueue or the last re-run), `LeaseToken`, `LeaseHolder varchar(64)` (`<machine>:<pid>`), `LeaseExpiresAt`, `CancelRequestedAt`, `CancelRequestedBy`, `StartedAt` (the latest attempt's start), `FinishedAt` (set with `Succeeded`, `DeadLettered` and `Cancelled`), `LastErrorCode varchar(128)`, `LastErrorMessage nvarchar(512)` | primary key `Id`; unique `(JobName, IdempotencyKey)`; `(Status, DueAt)` including `JobName` for the claim; `LeaseExpiresAt` where set, for recovery; `CreatedAt`, for the list; `FinishedAt` where set, for retention |
| `JobRunEvents` | `Id` (`JobRunEventId`, version 7), `RunId`, `OccurredAt`, `Kind tinyint`, `ActorId`, `Attempt`, `Runner varchar(64)`, `TraceId varchar(32)` (the attempt's W3C trace id, also the `CorrelationId` of its change records), `ErrorCode varchar(128)`, `ErrorMessage nvarchar(512)`, `RetryAt` | primary key `Id`; `RunId` → `JobRuns` with `ON DELETE CASCADE`; index `(RunId, OccurredAt)` |
| `JobSchedules` | `JobName varchar(128)`, `Expression varchar(200)` (the normalised expression `NextOccurrenceAt` was computed from), `NextOccurrenceAt`, `LastOccurrenceAt`, `LastRunId` | primary key `JobName`; `LastRunId` has no foreign key, so a deleted run reads as finished |

Every time is a UTC `datetimeoffset`. A run's status (`JobRunStatus`, a camelCase name on the wire):

| Status | Value | Meaning |
|---|---|---|
| `Pending` | 1 | waits for an attempt from `DueAt`: its first, or the next after an interruption or a re-run |
| `Running` | 2 | an attempt holds the lease `LeaseToken` until `LeaseExpiresAt` |
| `Failed` | 3 | its last attempt failed in a way that can heal, and the next starts at `DueAt` |
| `Succeeded` | 4 | finished: the job succeeded |
| `DeadLettered` | 5 | finished: its attempts ran out, or it failed in a way that cannot heal; an administrator acts |
| `Cancelled` | 6 | finished: an administrator cancelled it |

The moves: `Pending` or `Failed` → `Running` (a claim); `Running` → `Succeeded`, `Failed`, `DeadLettered`, `Pending` (interrupted) or `Cancelled`; `Running` → `Failed` or `DeadLettered` when its lease expired; `Pending` or `Failed` → `Cancelled`; `Failed`, `DeadLettered` or `Cancelled` → `Pending` (a re-run).

A run's events (`JobRunEventKind`): `Enqueued` 1, `Started` 2, `Succeeded` 3, `Failed` 4, `DeadLettered` 5, `Interrupted` 6, `LeaseExpired` 7, `CancelRequested` 8, `Cancelled` 9 and `Rerun` 10. Each carries its time and an actor: the person who asked for an enqueue, a cancel or a re-run, or the system for an occurrence and for an attempt's own events; an attempt's events also carry its number, the runner and its trace id, a failure its error, and a retry the time of the next attempt (`RetryAt`).

No entity is auditable ([ADR-0059](../adr/0059-background-jobs-a-persistent-queue-leases-ist-schedules-and-the-nightly-account-check.md)): every claim, renewal and outcome changes a run, and as `IAuditable` each would add a change record to the books' trail. `JobRunEvent` is `IAppendOnly`, inserted once and deleted only with its run, by the cascade; `JobRun` and `JobSchedule` are neither, so the bulk guard lets `JobRunTransitions` change them with `ExecuteUpdateAsync`, and neither is an aggregate root, because `AggregateTests` would require it to be auditable ([persistence](persistence.md), section "Auditing, soft delete and row versions"). The context maps `audit.ChangeRecords`, excluded from its migrations, as every context does. `RequestedBy`, `CancelRequestedBy` and `ActorId` hold an actor id (a person's Entra object id, `ActorIds.System` or `ActorIds.Anonymous`), named only when read. The business changes a job makes are recorded by the contexts that hold them, with the system as their actor.

## Defining a job

A job implements `IJob`, or `IJob<TPayload>` when each run carries a payload, and lives in its feature's `Hosting/` folder ([module anatomy](module-anatomy.md)), whose layer rule applies: Application, Domain, `BuildingBlocks.*` and `Microsoft.Extensions.*`, never ASP.NET Core, EF Core or Endpoints.

```csharp
internal sealed record OverdueReminder(InvoiceId InvoiceId, DateOnly DueDate);

internal sealed class OverdueReminderJob(InvoiceReminders reminders) : IJob<OverdueReminder>
{
    public static JobDefinition Definition { get; } = new(
        "finance.sales.overdue-reminder",
        "Overdue invoice reminder",
        "Sends the client of an overdue invoice one reminder for its due date.",
        JobRetryPolicy.Default,
        TimeSpan.FromMinutes(10));

    public Task<Result> RunAsync(OverdueReminder payload, JobContext context, CancellationToken cancellationToken) =>
        reminders.SendOnceAsync(payload.InvoiceId, payload.DueDate, cancellationToken);
}
```

`JobDefinition(Name, Title, Description, Retry, AttemptTimeout)`:

- `Name`: three lower-case kebab-case segments, at most 128 characters, the first two the module's permission prefix (`identity.users.directory-account-check`), and unique; the delivery job of an integration-event subscriber is named under the prefix of the module that subscribes ([integration events](integration-events.md), section "Subscribing"). `JobCatalog` refuses a duplicate name or job type when the module registers it, and `JobTests.JobNames_AreUniqueAndModuleJobsUseTheirModulesPrefix` checks every job.
- `Title` and `Description`: what the jobs page shows; the description is one sentence (`JobTests.JobDefinitions_DescribeEachJobInOneSentence`).
- `Retry`: `JobRetryPolicy(MaxAttempts, FirstDelay, MaxDelay)`, 1 to 20 attempts, `FirstDelay` from 1 second to 1 day, `MaxDelay` at least `FirstDelay` and at most 7 days; `JobRetryPolicy.Default` is (5, 1 minute, 1 hour), and `DelayAfter(failures)` gives the delay after a number of failures (section "Retries and the dead letter").
- `AttemptTimeout`: how long one attempt may run before its token is cancelled, 10 minutes for the account check.

The module registers the job from its `AddServices`. A job is never registered as a service: the runner creates it per run with `ActivatorUtilities.CreateFactory` in the run's scope, so its constructor may take a service only the API composes (`IDirectoryAccounts`), and the migrator and the test database host still compose the module. `JobTests.Jobs_CanEveryOneBeActivatedInTheApiHost` resolves every job's constructor in a scope of the API host.

```csharp
builder.Services.AddJob<OverdueReminderJob, OverdueReminder>(OverdueReminderJob.Definition);
```

`AddJob<TJob>(definition)` registers a job without a payload. A recurring job takes no payload and gets its schedule from an options class of its module, read on every poll, so its options are bound without validation:

```csharp
builder.Services.AddOptions<AccountCheckOptions>().BindConfiguration("Erp:Identity:Users");
builder.Services.AddRecurringJob<DirectoryAccountCheckJob, AccountCheckOptions>(DirectoryAccountCheckJob.Definition, options => options.AccountCheckSchedule);
```

The schedule's key follows the configuration rule `Erp:<Domain>:<Module>:<Setting>` (`Erp:Identity:Users:AccountCheckSchedule`), is seeded in `infra/appconfig/defaults.json` and is registered in the [configuration register](../configuration.md).

## Rules for every job

- **Idempotent and safe to run twice at once.** A run executes at least once: a process that dies after the work and before the outcome is recorded, a process paused past its lease while another runner recovers the run, and an administrator's re-run beside an occurrence all run the work again. The account check takes the organisation lock and reads only active records, so a second run changes nothing.
- **It reads its data when it runs.** The payload holds identifiers and dates only (section "Payloads and error text").
- **It honours its cancellation token.** The attempt timeout, a cancel request, a lost lease and the shutdown reach the job only through it, and .NET cannot stop code that ignores it: such a job keeps its slot and its lease until it returns.
- **It returns a `Result`.** The error's kind decides the retry: `Unavailable` and `Conflict` for a failure that can heal, any other kind for one that cannot. An exception is retried too, as `job.exception`.
- **Its error codes and messages are for an administrator**: they name no person, value or secret, because the jobs page shows them. It logs counts and ids, never names or the payload; the account check logs "The nightly account check read {CheckedCount} accounts and deactivated {DeactivatedCount} people".
- **It keeps no state between runs**: it is created per run.

## Enqueuing

`IJobQueue` enqueues a run and answers its `JobRunId`:

```csharp
await jobs.EnqueueAsync<OverdueReminderJob, OverdueReminder>(
    new OverdueReminder(invoice.Id, invoice.DueDate),
    $"invoice:{invoice.Id.Value}:due:{invoice.DueDate:yyyy-MM-dd}",
    cancellationToken);
```

`EnqueueAsync<TJob>(idempotencyKey, cancellationToken)` enqueues a job without a payload.

- The job must be registered, the key must be 1 to 200 characters of `A-Z a-z 0-9 . _ : + -` (no spaces and no `@`, so no email address fits; built from ids and dates, never names) and must not start with `occurrence:`, which belongs to the scheduler, and the payload must fit 16 KiB of UTF-8. Anything else throws: it is a programming error.
- `(JobName, IdempotencyKey)` is unique. An enqueue whose key exists answers the existing run's id, whatever its state, when the payload's JSON is identical, and throws `InvalidOperationException` when it differs. A key deduplicates for as long as its run is kept, 400 days after it finished (section "Retention").
- The run is `Pending`, due at once, with `RequestedBy` the current actor and its `Enqueued` event, and the enqueue wakes the runner of the same process (`JobSignal`).
- The enqueue commits on its own, through `JobsDbContext`'s connection, outside the caller's unit of work, so a run enqueued from a command exists even when the command then fails, throws or rolls back. Work that must run only once a change has committed is an integration event instead: the module publishes it inside its unit of work and subscribes to it itself, so the delivery's run is created only after the commit ([integration events](integration-events.md), section "Subscribing"; [ADR-0060](../adr/0060-integration-events-per-module-outboxes-deliveries-as-job-runs-and-no-order.md)). A direct enqueue uses a key derived from the change that asks for the job, so a retried unit of work cannot run it twice, and the job reads the current state when it runs.

## Payloads and error text

- A payload is serialised by `System.Text.Json` with the Web defaults, strict numbers and enums as camelCase names, into the `json` column `Payload`, at most 16 KiB of UTF-8 (`ArgumentException` above that), and read strictly: `JobPayloads` sets `RespectRequiredConstructorParameters` and `RespectNullableAnnotations`, so a payload that lacks a constructor parameter without a default, holds a null for a member that cannot be null or holds a value of the wrong type is unreadable, never filled with a default, while a member the type does not know is skipped. It holds identifiers and dates only, never a secret, a token, a credential or a connection string, nor personal data (names, emails, phone numbers, PAN, Aadhaar, bank details, salary). The API never returns it, the page never shows it, and no log or span carries it.
- A run whose payload does not deserialise, because its type changed since it was enqueued, fails permanently with `job.payload-unreadable`.
- A failed `Result` stores its `Error.Code` (at most 128 ASCII characters, else `job.failed`) and its `Error.Message`, cut to 512 characters.
- An exception stores `job.exception` and the exception's type name alone: an exception's message can quote a value (SQL Server's messages, [ADR-0054](../adr/0054-failed-sql-spans-rely-on-the-sqlclient-instrumentation.md)) or an address that carries a token. The exception itself goes only to the log, as `GlobalExceptionHandler` logs a request's, and no answer of the API echoes a payload or an exception's text.

## Claims, leases and one attempt

Each poll of the runner, every `PollInterval` or at once when `JobSignal` wakes it, enqueues the schedules' due occurrences, asks every run source (`IJobRunSource.EnqueueDueAsync`) for the runs it has to enqueue, recovers the expired leases of the jobs its process defines and claims due runs into its free slots (`MaxConcurrentRuns` less the attempts in flight). Each source runs in its own `try`/`catch` with a failure streak of its own, so a source that throws costs the poll its own work alone, and a run it enqueues is claimed in the same poll; the one source today is the outbox relay ([integration events](integration-events.md), section "The relay"). The runner reads its candidates without tracking: `Status` `Pending` or `Failed`, `DueAt` reached and `JobName` among the jobs its process defines, ordered by `DueAt` and `Id`.

The claim of each is one `ExecuteUpdateAsync`, in `JobRunTransitions`, whose `WHERE` repeats what the runner read: `Id`, `Status` `Pending` or `Failed`, `DueAt` reached and the `AttemptCount` it saw. It sets `Status` `Running`, a new `LeaseToken` (`Guid.CreateVersion7()`), `LeaseHolder`, `LeaseExpiresAt` (now plus `LeaseDuration`), `StartedAt` and `AttemptCount + 1`, and the `Started` event is inserted in the same transaction, under the execution strategy. One row changed is a claim; none means another runner won (`erp.jobs.claims.lost`). A single-row update whose `WHERE` repeats what was read changes the row for one of two racing runners only: under SQL Server's locking `READ COMMITTED` the update scans with update locks, and under Azure SQL's optimized locking it qualifies the row again when it changed after it qualified ([ADR-0059](../adr/0059-background-jobs-a-persistent-queue-leases-ist-schedules-and-the-nightly-account-check.md), "Context"). Every later write of the attempt also requires its `LeaseToken` and `Status` `Running`, so a runner that lost its lease never overwrites another's outcome. Claiming only the names its process defines leaves a run of a job another release defines to an instance that knows it.

One attempt:

1. The claim.
2. A log scope `{JobRunId, JobName, Attempt, CorrelationId}` and an `Activity` `job <name>` of the source `Dewiride.Erp.Jobs`, which the runner creates itself when no listener samples it, so every attempt has a trace id.
3. A scope of its own from the root `IServiceScopeFactory`, whose actor must be the system (section "Who a job runs as").
4. The job created in that scope (`ActivatorUtilities.CreateFactory`) and the payload deserialised.
5. The job's token, linked from the host's abort, the attempt timeout, a cancel request and a lost lease; the renewal loop started; the job invoked through `JobInvoker`.
6. The outcome classified (`JobAttemptOutcome`, section "Retries and the dead letter").
7. The outcome recorded by a compare-and-set on the lease, with its event, in one transaction.
8. The metrics raised and the loop woken for the freed slot.

While the job runs, the renewal loop extends the lease every third of `LeaseDuration` (40 seconds by default) and reads whether a cancel was requested. An attempt that ends before a renewal saw the request still finds it when it records its ending: a failed, dead-lettered or interrupted ending applies only while no stop is stored, else the run is recorded `Cancelled` under the same lease token in the same transaction. A renewal that finds another token in the row cancels the job's token and discards the outcome, logged at Warning (outcome `lease-lost`). A claim that commits as the shutdown begins is still read back, and its attempt runs as any other attempt in flight (section "Shutdown"): a job that returns before the host's shutdown timeout records its own outcome, and only a job still running when the timeout fires is interrupted, its run going back to `Pending`, due at once, without a failure. Each poll also recovers the runs of the jobs its process defines whose lease expired because their runner stopped renewing it (a killed process), by the same compare-and-set on the `LeaseToken` it read: a `LeaseExpired` event and the failure `job.lease-expired`, retried or dead-lettered as any other, or `Cancelled` when a cancel was requested, even one stored after the recovery read the run, its event then carrying `job.lease-expired` (`erp.jobs.leases.recovered`). Lease times come from each process's `TimeProvider`, so the instances' clocks must agree within seconds; production runs one instance on one server.

## Retries and the dead letter

| An attempt's outcome | Recorded | Then |
|---|---|---|
| `Result.Success()` | `Succeeded`, `FinishedAt`, event `Succeeded` | finished |
| a failed `Result` of kind `Unavailable` or `Conflict` | its code and message | retried |
| an exception | `job.exception` and the exception's type name | retried |
| the attempt timeout | `job.timed-out` | retried |
| an expired lease, found by recovery | `job.lease-expired`, event `LeaseExpired` | retried |
| a failed `Result` of any other kind (`Validation`, `NotFound`, `Forbidden`, `Failure`, `TooLarge`, `UnsupportedType`) | its code and message | dead-lettered at once |
| a payload that does not deserialise | `job.payload-unreadable` | dead-lettered at once |
| a scope whose actor is not the system | `job.actor-not-system` | dead-lettered at once |
| the shutdown cancelled the job | event `Interrupted` | `Pending`, due at once, `FailureCount` unchanged |
| a cancel request, the job stopping | `Cancelled`, `FinishedAt`, event `Cancelled` | finished |

Retried means: `FailureCount` grows by one; below `MaxAttempts` the run is `Failed`, due after `min(MaxDelay, FirstDelay × 2^(FailureCount − 1))`, with a `Failed` event whose `RetryAt` is that time; at `MaxAttempts` it is `DeadLettered`, with `FinishedAt` and a `DeadLettered` event. The default policy (5, 1 minute, 1 hour) waits 1, 2, 4 and 8 minutes and dead-letters at the fifth failure. The account check's (6, 5 minutes, 1 hour) starts its attempts at 00:30, 00:35, 00:45, 01:05, 01:45 and 02:45 India time and dead-letters a sixth failure before the 03:00 update restart. The delay has no jitter, so the page shows the exact time of the next attempt. A refusal that cannot change dead-letters at once, because retrying it only delays the dead letter that tells an administrator to act: `directory.access-denied` while `User.Read.All` is not granted. `AttemptCount` numbers the attempts over the run's life; `FailureCount` drives the policy and is reset by a re-run.

## Cancelling and running again

Both are administrators' actions on the jobs page, through `IJobAdministration` and the routes of `Platform/Jobs`:

| Run | Cancel (`CancelAsync`) | Run again (`RerunAsync`) |
|---|---|---|
| `pending` | `Cancelled` at once, `FinishedAt`, event `Cancelled` with the administrator | 409 `job.run-not-rerunnable` |
| `running`, its lease held | `CancelRequestedAt` and `CancelRequestedBy` set and a `CancelRequested` event; the holding runner sees it at its next renewal, within about 40 seconds, cancels the job's token and records `Cancelled` when the job returns, whether it failed, timed out or was interrupted, or `Succeeded` when the job succeeded first; asking again answers the same | 409 `job.run-not-rerunnable` |
| `running`, its lease expired | `Cancelled` at once | 409 `job.run-not-rerunnable` |
| `failed` | `Cancelled` at once | back to `Pending` in place, due at once, `FailureCount` 0, `FinishedAt`, the last error and the cancel columns cleared, a `Rerun` event with the administrator, and the local runner woken; 409 `job.not-defined` when this release does not define the job |
| `deadLettered`, `cancelled` | 409 `job.run-finished` | as for `failed` |
| `succeeded` | 409 `job.run-finished` | 409 `job.run-not-rerunnable`, because running it again could repeat its effects |

Run again answers 409 `job.not-defined` for a run of a job this release does not define, whatever its status, before any other refusal. Cancel answers 409 `concurrency.conflict` when the run kept changing through three attempts; the page shows that refusal beside the button and asks the person to reload the page and try again, because `cancelJobRun` refreshes the page only after a cancel succeeds. A re-run keeps the run's id and key, so an occurrence stays one run, and a cancelled run can run again, so a mistaken cancel can be undone. An unknown run answers 404 `job.run-not-found`.

## Schedules in India time

A recurring job's expression comes from its module's options, read through `IOptionsMonitor<TOptions>` at every poll on every instance, so a refreshed value applies without a restart.

| Field | Values |
|---|---|
| minute | 0–59 |
| hour | 0–23 |
| day of month | 1–31 |
| month | 1–12 |
| day of week | 0–7, 0 and 7 being Sunday (`5-7` is Friday to Sunday) |

- **Grammar.** Five fields separated by one or more spaces, spaces allowed at either end, at most 200 characters. A field is `*`, or a list of items separated by commas, each a number, a range `n-m`, a range with a step `n-m/s`, or `*/s`; a number or a step has one or two ASCII digits. Numbers only: no month or day names, no `n/s`, no `?`, `L`, `W` or `#`, no seconds field. `JobSchedules.Expression` holds the normalised text, the spaces collapsed and the tokens as typed (` 30  0 * * * ` is `30 0 * * *`).
- **Time.** India Standard Time, the fixed offset +05:30 of `IndianStandardTime.Offset`, with no daylight saving.
- **The days.** As Ubuntu's crontab(5) defines them: a day field is restricted unless its text starts with `*`. When both day fields are restricted, a day matches when either matches; otherwise it matches when both do, an unrestricted field matching every day. So `*/2` filters but counts as unrestricted:

  | Expression | Runs |
  |---|---|
  | `30 0 * * *` | every day at 00:30 |
  | `0 0 13 * 5` | on every 13th and every Friday |
  | `0 0 13 * *` | on every 13th |
  | `0 0 */2 * 1` | on the Mondays that are odd days (in November 2026 the 9th and the 23rd) |
  | `0 0 1-31/2 * 1` | on every odd day and every Monday |
  | `0 9-17/4 * * *` | at 09:00, 13:00 and 17:00 |
  | `0 0 31 4-12/2 *` | on 31 August, 31 October and 31 December |

- **The next occurrence.** `CronSchedule.NextAfter(instant)` answers UTC and is strictly after the instant: it takes the instant in India time, truncated to the minute, plus one minute, and moves the month, the day, the hour and the minute forward to the next that match. The search stops 400 years after its start, one cycle of the Gregorian calendar, within which every day that ever matches recurs: a 29 February that must also fall on a given weekday can be 40 years apart (`0 0 29 2 */7`, 2088 to 2128), so a valid expression always finds one sooner.
- **Refusals.** Each is `Error.Validation("job.schedule-invalid", <message>)`, the message the jobs page shows: an empty expression, a field count other than five, a field holding something that is not a number, a range, a step or `*` (month and day names included), a number out of its field's range, a range that starts after it ends, a step outside 1 to the field's count, a step after a single value, an empty list item, `*` inside a list, more than 200 characters, and a schedule that never runs (an unrestricted day of week and a day of month that exists in none of the months it names, February counted with 29 days: `0 0 30 2 *`).

The scheduler keeps each schedule's state in `JobSchedules`:

| Situation | What it does |
|---|---|
| first sight of the job | inserts the row with the next occurrence after now; a duplicate-key insert means another instance won and is ignored; no past occurrence runs |
| the expression changed (its normalised text differs from the row's) | moves the row, by compare-and-set, to the next occurrence after now under the new expression; no run |
| due (`NextOccurrenceAt` reached) | takes the latest occurrence not after now, then in one transaction moves the row to the occurrence after it, by compare-and-set on the `NextOccurrenceAt` and `Expression` it read (no row changed: another scheduler took the occurrence, and it rolls back), and, unless the run `LastRunId` names is `pending`, `running` or `failed` (a skip it logs), inserts the run `occurrence:<the occurrence in India time, yyyy-MM-ddTHH:mm+05:30>`, due at the occurrence, with its `Enqueued` event, actor the system, and sets `LastRunId` |
| an invalid expression | logs it at Error once per distinct value and leaves the row and the job alone until the value is fixed; the API keeps running, and the jobs page shows the reason |

So an occurrence is enqueued once, however many instances poll and across restarts: the row's lock serialises the schedulers, and the unique `(JobName, IdempotencyKey)` is the last guard. Of the occurrences missed while the API was down only the latest runs, at the first poll after it is back, and an occurrence is skipped while the schedule's previous run is unfinished, so a stuck or retrying run never piles up; an administrator's re-run can still overlap one. The schedule options carry no `ValidateOnStart`, because nothing alerts anyone ([ADR-0055](../adr/0055-the-production-host-restarts-an-unhealthy-container-and-alerts-nobody.md)) and a refused start at the 03:00 restart would keep the whole ERP down.

## Shutdown

`JobRunnerService` is a `BackgroundService` whose loop claims nothing before `IHostApplicationLifetime.ApplicationStarted`, so a host whose start fails never claims a run. When the host stops:

1. The stopping token ends the loop, so nothing more is claimed.
2. The overridden `StopAsync(cancellationToken)` awaits the attempts in flight, whose jobs keep their tokens.
3. When its token fires at the host's `ShutdownTimeout` (30 seconds, the .NET default, left unchanged), it cancels the runner's abort source, which fires every job's token.
4. It then gives the attempts 5 more seconds to record their outcome, on a token of their own, and logs at Warning the attempts still running.

An attempt the shutdown cancelled is `Interrupted`: its run returns to `Pending`, due at once, with `FailureCount` unchanged, because a restart is no failure. The compose `api` service sets `stop_grace_period: 45s` (`infra/compose/compose.yaml`), the 30 seconds, the recording and a margin, so `docker compose stop` (`erp-ai-pro.service`), `heal.sh`'s `docker restart` and a deployment's rollout give the API that long before SIGKILL; Docker's default is 10 seconds. A SIGKILL, an exit for lack of memory or a power cut leaves the run `Running` until its lease expires, up to `LeaseDuration` later, when recovery counts a failure and retries it.

## Who a job runs as

The runner creates each run's scope from the root `IServiceScopeFactory` on its own loop, started with the host and outside any request, so in the API host `HttpActorContext` gives `(ActorIds.System, false)` and `HttpRequestOrigin` gives `SystemRequestOrigin`, whose correlation id is the attempt's trace id ([persistence](persistence.md), section "Actors"). A job's change records therefore carry `ActorId` `ActorIds.System`, which the audit page names "The ERP" (`actorKind` `system`), and the `CorrelationId` of the attempt, which its `Started` event holds as `TraceId`, so a run's changes are found from the run and its run from a change. Before the job is invoked the runner reads `IActorContext.ActorId` in the run's scope, and any other actor fails the attempt permanently with `job.actor-not-system`: the books' trail never credits a person with the ERP's own work. Who enqueued, re-ran or cancelled a run is on the run's events, never the actor of the job's changes.

## Administration

`IJobAdministration` answers the routes of `Platform/Jobs` ([routing register](../routing.md)):

| Member | Answers |
|---|---|
| `ListJobsAsync(ct)` | every registered job as `JobDetails` (name, title, description, maximum attempts) with its `JobScheduleDetails` when it recurs: the configured expression, the next occurrence (the row's while its expression is the configured one, otherwise computed from the configured expression; null, with the parser's message as `Problem`, when the expression is invalid), and the last occurrence with its run and that run's status |
| `ListRunsAsync(ListRequest, ct)` | a page of `JobRunDetails`; sort over `createdAt` (default, newest first, the id breaking ties), `dueAt` and `finishedAt`; filter over `jobName` and `status` (equality), `idempotencyKey` (text, `contains` included), `createdAt` and `finishedAt` (comparable); `query.*` for anything else |
| `GetRunAsync(id, ct)` | `JobRunHistory`: the run and its events, oldest first; `job.run-not-found` |
| `RerunAsync(id, ct)`, `CancelAsync(id, ct)` | the run as changed (section "Cancelling and running again") |

`JobErrors` holds the codes: `job.run-not-found` (`NotFound`), `job.run-not-rerunnable`, `job.run-finished` and `job.not-defined` (`Conflict`). Actors are `JobActorDetails(Kind, Id, Name)`: a person named through `IPersonNames`, the system, or anonymous (`JobActorKind`), which the web app shows as the person's name, "The ERP" and "Someone not signed in". The flag `Erp.Modules.Platform.Jobs` gates the routes and pages only: the runner, the scheduler and the nightly check run while it is off.

The pages: `/platform/jobs` shows the card "Jobs" (each job's description, its schedule "(India time)", its next run in India time, its last run with its status and a link, and the reason an invalid schedule is not used) and the card "Runs", a list of the runs, 10 a page by default, filtered by job, status, key and creation date, each run with Run again and Cancel for the holders of their permissions, each confirmed first. `/platform/jobs/runs/[runId]` shows a run with its actions and its events as a timeline.

## Observability

- **Logs** (`[LoggerMessage]`), inside the attempt's scope, never with the payload, the idempotency key or an actor id: an attempt started, succeeded (with its time), failed (with its code and the next attempt's time, Warning), gave up (Error), threw (Warning, the exception's type and the exception), overran its timeout, was interrupted, lost its lease or was recovered after its lease expired, and a scope with another actor (Error); a failed poll (the first of a streak at Warning with the exception, the rest at Debug, the recovery at Information with their count); a failing run source, on a streak of its own named by the source's type (the same three levels); the scheduler's enqueued, skipped and changed occurrences, an invalid schedule (Error, once per value) and options it cannot read (Warning); re-runs, cancels and stop requests; the retention sweep's counts. The store's executed commands (EF Core's `CommandExecuted`, category `Microsoft.EntityFrameworkCore.Database.Command`) log at Debug, because the runner reads the store at every poll; a failed command and a connection error keep their own levels, and every other context's commands still log at Information.
- **Metrics**, meter `Dewiride.Erp.Jobs`: `erp.jobs.attempts`, `erp.jobs.attempt.duration`, `erp.jobs.attempts.active`, `erp.jobs.leases.recovered`, `erp.jobs.claims.lost`, `erp.jobs.occurrences` and `erp.jobs.enqueued` (tags and units in [observability](../guides/observability.md), section "Background jobs").
- **Traces**, activity source `Dewiride.Erp.Jobs`: one span `job <name>` per attempt, kind Internal, with the tags `erp.jobs.job`, `erp.jobs.run_id`, `erp.jobs.attempt`, `erp.jobs.outcome` and `erp.error.code`; a failed attempt's status is Error with the error code alone. The spans of the application pipeline and of SQL are its children.
- **Health**: readiness gains `database:platform_jobs`. The runner has no health check: nothing alerts anyone ([ADR-0055](../adr/0055-the-production-host-restarts-an-unhealthy-container-and-alerts-nobody.md)), and `Degraded` restarts no container.

## Retention

`JobRunRetentionSweep` (an `IRetentionSweep`) runs in the migrator alone: after the seeders of `migrate`, and on `retain`, which `erp-ai-pro-retain.timer` runs every night at 01:00 India time ([migrations guide](../guides/migrations.md), section "Retention"). It deletes the runs `Succeeded`, `DeadLettered` or `Cancelled` whose `FinishedAt` is older than `Erp:Platform:Jobs:FinishedRunRetention` (400 days), oldest first in statements of 100 runs by the filtered `FinishedAt` index (`JobRunRetentionSweep.BatchSize`, each committed on its own, because the cascade deletes each run's events in the same statement and 5,000 locks make SQL Server lock the whole table), their events by the cascade, and the `JobSchedules` rows of jobs this release does not schedule. Pending, running and failed runs are never deleted. A failed sweep is logged at Error and the next run deletes what it left; `retain` exits 1. The API deletes nothing in `platform_jobs`.

## Configuration

Section `Erp:Platform:Jobs`, and each recurring job's schedule in its module's section; the source of each value is in the [configuration register](../configuration.md). Every value is seeded unlabelled in `infra/appconfig/defaults.json`; the Default column is the value that applies when no source sets the key.

| Key | Default | Rule | Read by | Takes effect |
|---|---|---|---|---|
| `Erp:Platform:Jobs:PollInterval` | `00:00:30` | 1 second to 10 minutes | the API (`JobRunnerOptions`) | at the next start |
| `Erp:Platform:Jobs:LeaseDuration` | `00:02:00` | 30 seconds to 1 hour; renewed every third of it | the API (`JobRunnerOptions`) | at the next start |
| `Erp:Platform:Jobs:MaxConcurrentRuns` | `2` | 1 to 16 | the API (`JobRunnerOptions`) | at the next start |
| `Erp:Platform:Jobs:FinishedRunRetention` | `400.00:00:00` | 1 to 3650 days | the migrator (`JobsOptions`, validated in every host), and the API, whose deliveries of integration events refuse an event this old ([integration events](integration-events.md)) | at the migrator's next run; for deliveries, at the API's next start |
| `Erp:Identity:Users:AccountCheckSchedule` | — (seeded `30 0 * * *`; without it the nightly check is not scheduled) | a five-field expression (section "Schedules in India time"); an invalid one stops only the nightly check | the API's scheduler (`AccountCheckOptions`), at every poll | on refresh |

`HostOptions.ShutdownTimeout` keeps its 30-second default, and the jobs need no secret and no Key Vault reference.

## Testing a job

- **Module unit tests** use hand-rolled recording fakes, as they do for `IAttachmentService`: the Platform/Jobs handler tests use `RecordingJobAdministration`.
- **The real queue in a host test.** `ErpApiFactory` removes the hosted `JobRunnerService` unless the test calls `WithJobRunner()`, because every factory host of the process shares one test database and a loop in each would claim other tests' runs and fire the nightly check at 00:30 India time; the engine's singletons stay. `JobRunnerDriver(IServiceProvider)` (`Dewiride.Erp.Testing.Jobs`) drives that engine: `RunAsync(JobRunId, CancellationToken)` runs one attempt of the run by its id through `JobRunner.TryRunAsync`, answering `JobRunOutcome(Status, ErrorCode, TraceId)`, and `EnqueueDueOccurrencesAsync(CancellationToken)` and `RecoverExpiredLeasesAsync(CancellationToken)` run those steps once. It claims by id because every host of the process shares the test database.
- **The in-process fake.** `ErpApiFactory.WithInProcessJobQueue()` registers `InProcessJobQueue` as the host's `IJobQueue`; it validates exactly as the real queue (the job registered, the key's grammar, the reserved prefix, the payload's size), records each enqueue, deduplicates by key, throws on a different payload and runs nothing until `RunAllAsync(cancellationToken)`, which runs each recorded run once through the building block's `JobInvoker`, in its own scope, as the system, and answers `InProcessJobRun` records with their `Result`.
- **Runs in a chosen state.** `JobRunSeeder` and `JobRunSeed` insert a run with consistent events; the end-to-end API host maps `POST /api/__test/jobs/runs` in `Testing` alone, which the Playwright fixture `fixtures/job-runs.ts` calls, each spec under a key prefix of its own. For the jobs card the same host registers, through `ErpApiFactory.WithTestServices`, the test-only recurring jobs `test.jobs.end-to-end-scheduled` (`15 2 * * *`) and `test.jobs.end-to-end-misscheduled` (`61 * * * *`), and `POST /api/__test/jobs/schedules` writes one of their `JobSchedules` rows, the first seed of each winning for the host's life; the account check's row is never seeded.
- **Time.** Every wait is `Task.Delay(…, TimeProvider, …)`, so a test moves a `FakeTimeProvider` instead of sleeping; rows a sweep may see are dated in 2001, as `AttachmentsSweeperTests` dates them.
- **Example.** `DirectoryAccountCheckJobTests` of the users module run the account check through `JobRunnerDriver` against an account `TestDirectory` reports disabled, and assert the person `AccountDisabled`, the change record's `ActorId` `ActorIds.System`, its `CorrelationId` equal to the attempt's trace id, and the audit API's `actorKind` `system`.

## Tests

| Project | Covers |
|---|---|
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.UnitTests/Jobs` | the cron parser's refusals and acceptances (`CronScheduleParseTests`) and next occurrences (`CronScheduleNextTests`: month ends, 29 February, the day of month with the day of week, UTC input), occurrence keys, the retry policy's delays and bounds, job names, keys and payloads (`JobPayloadsTests`, a payload read strictly included), the classification of every outcome (`JobAttemptOutcomeTests`), the catalogue's refusals, the options' ranges and the metrics |
| `Tests/BuildingBlocks/Dewiride.Erp.BuildingBlocks.IntegrationTests/Jobs` (SQL Server; the fixture `JobsDatabase` builds two independent providers over the test database, each with its own `FakeTimeProvider` from the same start, moved together by `Advance` or one alone, two runner instances and job names of their own, `test.jobs.<scenario>`) | the queue (`SqlJobQueueTests`, and `JobQueueContractTests` over it and `InProcessJobQueue`), two runners racing for one run and for fifty (`JobRunnerRaceTests`: one execution and one `Started` event per run), the claim's refusals and a claim committed as the shutdown begins (`JobClaimTests`), leases and their renewal (`JobLeaseTests`), retries, the dead letter and no claim before a retry is due (`JobRetryTests`), cancels and re-runs, a stop request winning over a failure, an interruption and a recovery (`JobCancellationTests`), the attempts gauge and an unrecorded ending (`JobAttemptTelemetryTests`), the shutdown, the `ApplicationStarted` gate and the run sources, a source's run claimed and run and a failing source's streak while the runner keeps claiming (`JobRunnerServiceTests`), the scheduler (`JobSchedulerTests`: first sight, one run per occurrence, the next occurrence after the previous run ended, two schedulers at once, a restart, three days down, a changed expression, an unfinished previous run, an invalid expression), the administration (`JobAdministrationTests`) and retention (`JobRunRetentionSweepTests`) |
| `Tests/Host` | the readiness checks; the runner registered in the API and its loop removed by the factory, the loop run with `WithJobRunner()`, and a job running as the system with its attempt's trace id as correlation id (`JobRunnerCompositionTests`); the in-process queue in a host (`InProcessJobQueueTests`); the end-to-end host's seed routes (`JobRunsTests`, `JobSchedulesTests`); the migrator migrating `platform_jobs`, and `retain` deleting the job runs past their retention while keeping the schedules of the modules' jobs |
| `Tests/Architecture` | every job activatable in the API host, unique names under their module's prefix and one-sentence descriptions (`JobTests`); `AddErpJobRunner` called by the API host alone (`LayerTests.JobRunner_IsComposedByTheApiHostAlone`); the module, its routes' permissions and its context through the catalogue, persistence and change-record tests |
| `Modules/Platform/Jobs/Tests` | each handler (unit, `RecordingJobAdministration`); every route end to end, the permission of each route and the feature gate on all five, the runner unaffected (integration) |
| `Modules/Identity/Users/Tests/IntegrationTests` | the nightly job deactivating a person whose account is disabled, as the ERP, and dead-lettering with `directory.access-denied` when `TestDirectory` refuses application tokens (`DirectoryAccountCheckJobTests`) |
| `frontend/e2e/tests/platform/jobs` | the pages as an administrator on all six projects in both themes: the jobs card with the next 00:30, the list's filters, sorting, page size and pages, the key filter refusing other characters, Run again and Cancel, a stale page's 409, the run's timeline and the not-found page; an employee's no-access page and 403 on all five routes; the disabled module and the page without an API |
