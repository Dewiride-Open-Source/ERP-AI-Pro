using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

public sealed record JobScheduleDetails(
    string Expression,
    DateTimeOffset? NextOccurrenceAt,
    string? Problem,
    DateTimeOffset? LastOccurrenceAt,
    JobRunId? LastRunId,
    JobRunStatus? LastRunStatus);
