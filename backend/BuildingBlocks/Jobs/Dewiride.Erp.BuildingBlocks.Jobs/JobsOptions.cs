using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Jobs;

public sealed class JobsOptions
{
    public const string SectionName = "Erp:Platform:Jobs";

    [Range(typeof(TimeSpan), "1.00:00:00", "3650.00:00:00")]
    public TimeSpan FinishedRunRetention { get; set; } = TimeSpan.FromDays(400);
}
