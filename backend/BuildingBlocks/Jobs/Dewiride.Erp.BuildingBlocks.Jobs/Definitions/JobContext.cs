using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

public sealed record JobContext(JobRunId RunId, int Attempt);
