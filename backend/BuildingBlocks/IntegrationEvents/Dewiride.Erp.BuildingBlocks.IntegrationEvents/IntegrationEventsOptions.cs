using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents;

public sealed class IntegrationEventsOptions
{
    public const string SectionName = "Erp:Platform:IntegrationEvents";

    [Range(typeof(TimeSpan), "1.00:00:00", "3650.00:00:00")]
    public TimeSpan PublishedEventRetention { get; set; } = TimeSpan.FromDays(400);
}
