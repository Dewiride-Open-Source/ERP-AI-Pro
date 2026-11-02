using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Scheduling;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

// What an administrator sees and does with the jobs. A re-run puts the same run back, due at once and with its failures counted
// from zero, and is refused for a run that succeeded, which could repeat what it did. A waiting run is cancelled at once; a
// running one is asked to stop, which its runner sees at its next lease renewal, and one whose runner stopped renewing is
// cancelled at once. Who asked is recorded on the run's events, never as the actor of what the job changes. The list sorts and
// filters on a run's own columns only, and never shows a payload.
internal sealed partial class JobAdministration : IJobAdministration
{
    private const int MaxCancelAttempts = 3;

    private static readonly SortableFields<JobRun> Sortable = new SortableFields<JobRun>()
        .Add("createdAt", r => r.CreatedAt)
        .Add("dueAt", r => r.DueAt)
        .Add("finishedAt", r => r.FinishedAt);

    private static readonly FilterableFields<JobRun> Filterable = new FilterableFields<JobRun>()
        .Add("jobName", r => r.JobName, FilterOperators.Equality)
        .Add("status", r => r.Status, FilterOperators.Equality)
        .Add("idempotencyKey", r => r.IdempotencyKey, FilterOperators.Text)
        .Add("createdAt", r => r.CreatedAt, FilterOperators.Comparable)
        .Add("finishedAt", r => r.FinishedAt, FilterOperators.Comparable);

    private static readonly SortRequest NewestFirst = new([new SortTerm("createdAt", SortDirection.Descending)]);

    private readonly JobsDbContext _context;

    private readonly JobCatalog _catalog;

    private readonly JobSignal _signal;

    private readonly IActorContext _actor;

    private readonly IReadOnlyList<IPersonNames> _personNames;

    private readonly IServiceProvider _services;

    private readonly TimeProvider _timeProvider;

    private readonly ILogger<JobAdministration> _logger;

    public JobAdministration(
        JobsDbContext context,
        JobCatalog catalog,
        JobSignal signal,
        IActorContext actor,
        IEnumerable<IPersonNames> personNames,
        IServiceProvider services,
        TimeProvider timeProvider,
        ILogger<JobAdministration> logger)
    {
        ArgumentNullException.ThrowIfNull(personNames);

        _context = context;
        _catalog = catalog;
        _signal = signal;
        _actor = actor;
        _personNames = [.. personNames];
        _services = services;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobDetails>> ListJobsAsync(CancellationToken cancellationToken)
    {
        var rows = (await _context.Schedules.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(row => row.JobName, StringComparer.Ordinal);
        var lastRunIds = rows.Values.Select(row => row.LastRunId).OfType<JobRunId>().ToList();
        IReadOnlyDictionary<JobRunId, JobRunStatus> statuses = lastRunIds.Count == 0
            ? new Dictionary<JobRunId, JobRunStatus>()
            : await _context.Runs.AsNoTracking().Where(r => lastRunIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Status, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();

        return
        [
            .. _catalog.Jobs.Select(registration => new JobDetails(
                registration.Name,
                registration.Definition.Title,
                registration.Definition.Description,
                registration.Definition.Retry.MaxAttempts,
                registration.IsRecurring ? Schedule(registration, rows.GetValueOrDefault(registration.Name), statuses, now) : null)),
        ];
    }

    public async Task<Result<PagedResult<JobRunDetails>>> ListRunsAsync(ListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sort = (request.Sort.Terms.Count == 0 ? NewestFirst : request.Sort).Resolve(Sortable);
        if (sort.IsFailure)
        {
            return sort.Error!;
        }

        var filter = request.Filter.Resolve(Filterable);
        if (filter.IsFailure)
        {
            return filter.Error!;
        }

        var page = await _context.Runs
            .AsNoTracking()
            .ApplyFilter(filter.Value)
            .ApplySort(sort.Value, r => r.Id)
            .ToPagedResultAsync(request.Page, cancellationToken)
            .ConfigureAwait(false);
        var names = await JobActorNames.ResolveAsync(page.Items.Select(run => run.RequestedBy), _personNames, cancellationToken).ConfigureAwait(false);

        return new PagedResult<JobRunDetails>([.. page.Items.Select(run => Details(run, names))], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<JobRunHistory>> GetRunAsync(JobRunId id, CancellationToken cancellationToken)
    {
        var run = await _context.Runs.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return JobErrors.RunNotFound;
        }

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.RunId == id)
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var names = await JobActorNames.ResolveAsync(events.Select(e => e.ActorId).Append(run.RequestedBy), _personNames, cancellationToken).ConfigureAwait(false);

        return new JobRunHistory(
            Details(run, names),
            [
                .. events.Select(e => new JobRunEventDetails(
                    e.Id.Value,
                    e.OccurredAt,
                    e.Kind,
                    names.Of(e.ActorId),
                    e.Attempt,
                    e.Runner,
                    e.TraceId,
                    e.ErrorCode,
                    e.ErrorMessage,
                    e.RetryAt)),
            ]);
    }

    public async Task<Result<JobRunDetails>> RerunAsync(JobRunId id, CancellationToken cancellationToken)
    {
        var run = await _context.Runs.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return JobErrors.RunNotFound;
        }

        if (_catalog.Find(run.JobName) is null)
        {
            return JobErrors.JobNotDefined;
        }

        if (run.Status is not (JobRunStatus.Failed or JobRunStatus.DeadLettered or JobRunStatus.Cancelled))
        {
            return JobErrors.RunNotRerunnable;
        }

        if (!await JobRunTransitions.RerunAsync(_context, id, _timeProvider.GetUtcNow(), _actor.ActorId, cancellationToken).ConfigureAwait(false))
        {
            return await _context.Runs.AnyAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false) ? JobErrors.RunNotRerunnable : JobErrors.RunNotFound;
        }

        LogRerun(run.JobName);
        _signal.Wake();

        return await DetailsAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<JobRunDetails>> CancelAsync(JobRunId id, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCancelAttempts; attempt++)
        {
            var run = await _context.Runs.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                return JobErrors.RunNotFound;
            }

            var now = _timeProvider.GetUtcNow();
            switch (run.Status)
            {
                case JobRunStatus.Succeeded or JobRunStatus.DeadLettered or JobRunStatus.Cancelled:
                    return JobErrors.RunFinished;
                case JobRunStatus.Pending or JobRunStatus.Failed:
                    if (await JobRunTransitions.CancelWaitingAsync(_context, id, now, _actor.ActorId, cancellationToken).ConfigureAwait(false))
                    {
                        LogCancelled(run.JobName);
                        return await DetailsAsync(id, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case JobRunStatus.Running when run.LeaseExpiresAt <= now:
                    if (await JobRunTransitions.CancelAbandonedAsync(_context, run, now, _actor.ActorId, cancellationToken).ConfigureAwait(false))
                    {
                        LogCancelled(run.JobName);
                        return await DetailsAsync(id, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case JobRunStatus.Running when run.CancelRequestedAt is not null:
                    return await DetailsAsync(id, cancellationToken).ConfigureAwait(false);
                default:
                    if (await JobRunTransitions.RequestCancelAsync(_context, id, now, _actor.ActorId, cancellationToken).ConfigureAwait(false))
                    {
                        LogStopRequested(run.JobName);
                        return await DetailsAsync(id, cancellationToken).ConfigureAwait(false);
                    }

                    break;
            }
        }

        return Error.Conflict(EfUnitOfWork.ConcurrencyConflictCode, "The run kept changing while it was being cancelled; reload it and try again.");
    }

    private async Task<Result<JobRunDetails>> DetailsAsync(JobRunId id, CancellationToken cancellationToken)
    {
        var run = await _context.Runs.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return JobErrors.RunNotFound;
        }

        var names = await JobActorNames.ResolveAsync([run.RequestedBy], _personNames, cancellationToken).ConfigureAwait(false);

        return Details(run, names);
    }

    private JobRunDetails Details(JobRun run, JobActorNames names)
    {
        var definition = _catalog.Find(run.JobName)?.Definition;

        return new JobRunDetails(
            run.Id,
            run.JobName,
            definition?.Title,
            run.IdempotencyKey,
            run.Status,
            run.DueAt,
            run.CreatedAt,
            names.Of(run.RequestedBy),
            run.AttemptCount,
            run.FailureCount,
            definition?.Retry.MaxAttempts,
            run.StartedAt,
            run.FinishedAt,
            run.CancelRequestedAt,
            run.LastErrorCode,
            run.LastErrorMessage);
    }

    private JobScheduleDetails Schedule(JobRegistration registration, JobSchedule? row, IReadOnlyDictionary<JobRunId, JobRunStatus> statuses, DateTimeOffset now)
    {
        JobRunStatus? lastRunStatus = row?.LastRunId is { } lastRunId && statuses.TryGetValue(lastRunId, out var status) ? status : null;
        string? configured;
        try
        {
            configured = registration.Schedule!(_services);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogScheduleUnreadable(exception, registration.Name);
            return new JobScheduleDetails(string.Empty, null, "The schedule setting cannot be read.", row?.LastOccurrenceAt, row?.LastRunId, lastRunStatus);
        }

        var parsed = CronSchedule.Parse(configured);
        if (parsed.IsFailure)
        {
            return new JobScheduleDetails(configured ?? string.Empty, null, parsed.Error!.Message, row?.LastOccurrenceAt, row?.LastRunId, lastRunStatus);
        }

        var schedule = parsed.Value;
        var next = row is not null && string.Equals(row.Expression, schedule.Expression, StringComparison.Ordinal) ? row.NextOccurrenceAt : schedule.NextAfter(now);

        return new JobScheduleDetails(schedule.Expression, next, null, row?.LastOccurrenceAt, row?.LastRunId, lastRunStatus);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "A run of {JobName} was put back to run again at an administrator's request")]
    private partial void LogRerun(string jobName);

    [LoggerMessage(Level = LogLevel.Information, Message = "A run of {JobName} was cancelled at an administrator's request")]
    private partial void LogCancelled(string jobName);

    [LoggerMessage(Level = LogLevel.Information, Message = "A running run of {JobName} was asked to stop at an administrator's request")]
    private partial void LogStopRequested(string jobName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The schedule of {JobName} could not be read from its options")]
    private partial void LogScheduleUnreadable(Exception exception, string jobName);
}
