using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Erp:Platform:Database";

    public string? ConnectionString { get; set; }

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    [Range(0, 20)]
    public int MaxRetryCount { get; set; } = 5;

    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(10);
}
