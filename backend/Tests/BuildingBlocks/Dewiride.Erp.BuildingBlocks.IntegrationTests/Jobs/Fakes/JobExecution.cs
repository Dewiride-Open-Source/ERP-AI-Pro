using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

public sealed record JobExecution(JobRunId RunId, int Attempt, Guid ActorId, string? CorrelationId);
