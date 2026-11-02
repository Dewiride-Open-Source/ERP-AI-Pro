using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;

internal sealed record ClaimedRun(
    JobRunId Id,
    string JobName,
    string? Payload,
    int Attempt,
    int FailureCount,
    Guid LeaseToken,
    string Runner,
    string TraceId,
    DateTimeOffset StartedAt);
