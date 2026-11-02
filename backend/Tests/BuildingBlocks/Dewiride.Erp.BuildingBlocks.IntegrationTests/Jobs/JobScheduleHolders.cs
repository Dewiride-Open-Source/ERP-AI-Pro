namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs;

// The retention sweep deletes the schedule rows of every job its host does not schedule, which in the shared test database are
// the rows of every other test, so the tests that keep schedule rows run alone, after the collections that run in parallel.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class JobScheduleHolders
{
    public const string Name = "Job schedules";
}
