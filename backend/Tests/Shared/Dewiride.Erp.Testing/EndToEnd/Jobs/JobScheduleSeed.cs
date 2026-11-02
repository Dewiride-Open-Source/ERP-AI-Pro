namespace Dewiride.Erp.Testing.EndToEnd.Jobs;

public sealed record JobScheduleSeed(string? JobName, Guid? LastRunId, DateTimeOffset? LastOccurrenceAt);
