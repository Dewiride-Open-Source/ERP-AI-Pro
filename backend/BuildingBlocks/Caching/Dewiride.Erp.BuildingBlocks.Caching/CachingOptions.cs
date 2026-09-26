using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Caching;

public sealed class CachingOptions
{
    public const string SectionName = "Erp:Platform:Caching";

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    [Range(typeof(long), "1024", "67108864")]
    public long MaximumPayloadBytes { get; set; } = 1024 * 1024;
}
