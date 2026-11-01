using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running;

public sealed class JobRunnerOptions
{
    public const string SectionName = JobsOptions.SectionName;

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    // Renewed every third of its length, so an attempt keeps its lease through a renewal that fails, and another runner takes
    // the run over only once the runner holding it has missed two renewals in a row.
    [Range(typeof(TimeSpan), "00:00:30", "01:00:00")]
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    [Range(1, 16)]
    public int MaxConcurrentRuns { get; set; } = 2;

    public TimeSpan RenewalInterval => LeaseDuration / 3;
}
