using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Testing.Jobs;

public sealed record JobRunOutcome(JobRunStatus Status, string? ErrorCode, string TraceId);
