using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Testing.Jobs;

// Attempts counts the attempts a running, failed, succeeded or dead-lettered run has started, every earlier one failed with the
// seed's error; a pending or cancelled run has started none. StopRequested asks a running run to stop.
public sealed record JobRunSeed(string JobName, string IdempotencyKey, JobRunStatus Status)
{
    public const string DefaultErrorCode = "job.failed";

    public const string DefaultErrorMessage = "The attempt failed.";

    public int Attempts { get; init; } = 1;

    public string ErrorCode { get; init; } = DefaultErrorCode;

    public string ErrorMessage { get; init; } = DefaultErrorMessage;

    public bool StopRequested { get; init; }

    public Guid RequestedBy { get; init; } = ActorIds.System;
}
