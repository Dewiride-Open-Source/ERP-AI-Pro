namespace Dewiride.Erp.BuildingBlocks.Jobs.Administration;

public sealed record JobDetails(string Name, string Title, string Description, int MaxAttempts, JobScheduleDetails? Schedule);
