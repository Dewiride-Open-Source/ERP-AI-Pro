using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;

/// <summary>The schedule of a recurring job, with the latest occurrence the ERP enqueued a run for.</summary>
/// <param name="Expression">Configured five-field cron expression in India Standard Time (minute, hour, day of month, month and day of week), normalised when it is valid and as configured when it is not; empty when no expression is configured or the setting cannot be read.</param>
/// <param name="NextOccurrenceAt">UTC time of the next occurrence; null when the job is not scheduled, and then Problem says why.</param>
/// <param name="Problem">Why the job is not scheduled, such as what is wrong with the expression; null while the schedule is valid.</param>
/// <param name="LastOccurrenceAt">UTC time of the latest occurrence the ERP enqueued a run for; null until the first.</param>
/// <param name="LastRunId">Identifier of the run of that occurrence; null until the first.</param>
/// <param name="LastRunStatus">Where the run of that occurrence stands; null until the first occurrence and once that run has been deleted after the retention period.</param>
internal sealed record JobScheduleResponse(
    string Expression,
    DateTimeOffset? NextOccurrenceAt,
    string? Problem,
    DateTimeOffset? LastOccurrenceAt,
    Guid? LastRunId,
    JobRunStatus? LastRunStatus);
