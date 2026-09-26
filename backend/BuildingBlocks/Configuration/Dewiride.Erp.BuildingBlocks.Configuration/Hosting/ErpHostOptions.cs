using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Configuration.Hosting;

public sealed class ErpHostOptions
{
    public const string SectionName = "Erp:Platform:Host";

    [Required]
    [MinLength(1)]
    public string ApplicationName { get; set; } = "ERP-AI-Pro";

    [Required]
    [MinLength(1)]
    public string AllowedHosts { get; set; } = "*";

    public IReadOnlyList<string> KnownNetworks { get; set; } = [];

    [Range(typeof(long), "1024", "1073741824")]
    public long MaxRequestBodyBytes { get; set; } = 1024 * 1024;

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
