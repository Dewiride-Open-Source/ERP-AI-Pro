using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

public sealed record JobRunDetails(
    JobRunId Id,
    string JobName,
    string? JobTitle,
    string IdempotencyKey,
    JobRunStatus Status,
    DateTimeOffset DueAt,
    DateTimeOffset CreatedAt,
    JobActorDetails RequestedBy,
    int AttemptCount,
    int FailureCount,
    int? MaxAttempts,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? CancelRequestedAt,
    string? LastErrorCode,
    string? LastErrorMessage);
