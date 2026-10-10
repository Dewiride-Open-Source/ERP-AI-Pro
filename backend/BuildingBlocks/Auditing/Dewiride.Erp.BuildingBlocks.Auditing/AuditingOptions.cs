using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Auditing;

public sealed class AuditingOptions
{
    public const string SectionName = "Erp:Platform:Auditing";

    [Range(typeof(TimeSpan), "1.00:00:00", "3650.00:00:00")]
    public TimeSpan SecurityEventRetention { get; set; } = TimeSpan.FromDays(365);

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromDays(1);
}
