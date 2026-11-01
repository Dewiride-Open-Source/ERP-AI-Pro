using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Administration;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;

namespace Dewiride.Erp.Modules.Platform.Jobs.UnitTests.Runs.Application;

internal static class JobRunSamples
{
    public const string JobName = "identity.users.directory-account-check";

    public static readonly DateTimeOffset OccurrenceAt = new(2026, 11, 1, 19, 0, 0, TimeSpan.Zero);

    public static JobRunDetails Run(JobRunStatus status) =>
        new(
            JobRunId.Create(),
            JobName,
            "Microsoft account check",
            "occurrence:2026-11-02T00:30+05:30",
            status,
            OccurrenceAt,
            OccurrenceAt,
            new JobActorDetails(JobActorKind.System, ActorIds.System, null),
            1,
            status is JobRunStatus.Failed or JobRunStatus.DeadLettered ? 1 : 0,
            6,
            OccurrenceAt,
            status is JobRunStatus.Succeeded or JobRunStatus.DeadLettered or JobRunStatus.Cancelled ? OccurrenceAt : null,
            status == JobRunStatus.Cancelled ? OccurrenceAt : null,
            status is JobRunStatus.Failed or JobRunStatus.DeadLettered ? "directory.unavailable" : null,
            status is JobRunStatus.Failed or JobRunStatus.DeadLettered ? "The company directory cannot answer now." : null);
}
