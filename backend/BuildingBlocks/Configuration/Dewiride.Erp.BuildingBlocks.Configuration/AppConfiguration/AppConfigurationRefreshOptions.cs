using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;

public sealed class AppConfigurationRefreshOptions
{
    public const string SectionName = "Erp:Platform:Configuration";

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(30);

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan SecretRefreshInterval { get; set; } = TimeSpan.FromHours(1);

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromMinutes(1);
}
