using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Idempotency;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Erp:Platform:Idempotency";

    [Range(typeof(TimeSpan), "00:05:00", "30.00:00:00")]
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(1);

    [Range(1024, 16 * 1024 * 1024)]
    public int MaxStoredResponseBytes { get; set; } = 1024 * 1024;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
