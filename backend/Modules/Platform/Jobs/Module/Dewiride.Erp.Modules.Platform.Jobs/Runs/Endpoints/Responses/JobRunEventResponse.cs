using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Modules.Platform.Jobs.Runs.Endpoints.Responses;

/// <summary>One step in the history of a job run.</summary>
/// <param name="Id">Identifier of the step.</param>
/// <param name="OccurredAt">UTC time of the step.</param>
/// <param name="Kind">What happened: the run was enqueued, an attempt started, succeeded, failed, gave up, was interrupted by a shutdown or lost its lease, someone asked the run to stop, cancelled it or put it back to run again.</param>
/// <param name="Actor">Who took the step: the ERP itself for the steps of an attempt, the one who enqueued the run, or the administrator who re-ran or cancelled it.</param>
/// <param name="Attempt">Number of the attempt the step belongs to, counted over the run's life; null for a step that belongs to no attempt, such as the enqueue or a re-run.</param>
/// <param name="Runner">Process that held the attempt, its machine name and process id joined by a colon; null for a step that belongs to no attempt.</param>
/// <param name="TraceId">W3C trace id of the attempt, which its log lines and the change records it wrote carry as their correlation id; null for a step no attempt took.</param>
/// <param name="ErrorCode">Code of the error the step recorded, such as job.exception; null for a step without an error.</param>
/// <param name="ErrorMessage">Message of that error, written for an administrator; null for a step without an error.</param>
/// <param name="RetryAt">UTC time the run tries again after the attempt this step failed; null when the step schedules no retry.</param>
internal sealed record JobRunEventResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    JobRunEventKind Kind,
    JobActorResponse Actor,
    int? Attempt,
    string? Runner,
    string? TraceId,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset? RetryAt);
