using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;

/// <summary>A run of a job: one piece of work, with its attempts, its latest error and who asked for it.</summary>
/// <param name="Id">Identifier of the run.</param>
/// <param name="JobName">Name of the run's job.</param>
/// <param name="JobTitle">Title of the run's job; null when this release of the ERP does not define the job.</param>
/// <param name="IdempotencyKey">Key that makes the run unique among the runs of its job, built from ids and dates; a scheduled run's key is occurrence: followed by the occurrence's time in India Standard Time.</param>
/// <param name="Status">Where the run stands: waiting, running, failed and due to try again, succeeded, given up or cancelled.</param>
/// <param name="DueAt">UTC time from which the run's next attempt may start, which for a failed run is when it tries again; a finished run keeps the time its last attempt was due.</param>
/// <param name="CreatedAt">UTC time the run was enqueued.</param>
/// <param name="RequestedBy">Who enqueued the run: the person whose request enqueued it, or the ERP itself, as for a scheduled run.</param>
/// <param name="AttemptCount">Attempts the run has started over its life, those before a re-run included.</param>
/// <param name="FailureCount">Attempts that failed since the run was enqueued or last re-run, which decide when it gives up.</param>
/// <param name="MaxAttempts">Attempts the run makes before it gives up; null when this release of the ERP does not define its job.</param>
/// <param name="StartedAt">UTC time the run's latest attempt started; null before its first attempt.</param>
/// <param name="FinishedAt">UTC time the run succeeded, gave up or was cancelled; null while it is unfinished.</param>
/// <param name="CancelRequestedAt">UTC time an administrator cancelled the run or asked it to stop; null when nobody has since the run was enqueued or last re-run.</param>
/// <param name="LastErrorCode">Code of the error of the latest failed attempt, such as job.exception or job.timed-out; null when no attempt has failed since the run was enqueued, last re-run or last succeeded.</param>
/// <param name="LastErrorMessage">Message of that error, written for an administrator; null when LastErrorCode is null.</param>
internal sealed record JobRunResponse(
    Guid Id,
    string JobName,
    string? JobTitle,
    string IdempotencyKey,
    JobRunStatus Status,
    DateTimeOffset DueAt,
    DateTimeOffset CreatedAt,
    JobActorResponse RequestedBy,
    int AttemptCount,
    int FailureCount,
    int? MaxAttempts,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? CancelRequestedAt,
    string? LastErrorCode,
    string? LastErrorMessage);
