using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "Erp:Platform:RateLimiting";

    public bool Enabled { get; set; } = true;

    [Range(1, 1_000_000)]
    public int AnonymousPermitLimit { get; set; } = 600;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan AnonymousWindow { get; set; } = TimeSpan.FromMinutes(1);

    [Range(1, 1_000_000)]
    public int ActorPermitLimit { get; set; } = 600;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan ActorWindow { get; set; } = TimeSpan.FromMinutes(1);

    [Range(1, 60)]
    public int ActorSegmentsPerWindow { get; set; } = 6;
}
