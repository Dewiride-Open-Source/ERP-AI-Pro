using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

public sealed record SampleRenamed(SampleId SampleId, string OldName, string NewName, DateTimeOffset OccurredOn) : IDomainEvent;
