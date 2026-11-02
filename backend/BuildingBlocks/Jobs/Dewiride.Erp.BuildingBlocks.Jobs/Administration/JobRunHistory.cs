namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

public sealed record JobRunHistory(JobRunDetails Run, IReadOnlyList<JobRunEventDetails> Events);
