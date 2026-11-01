using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

public sealed record JobRunEventDetails(
    Guid Id,
    DateTimeOffset OccurredAt,
    JobRunEventKind Kind,
    JobActorDetails Actor,
    int? Attempt,
    string? Runner,
    string? TraceId,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset? RetryAt);
