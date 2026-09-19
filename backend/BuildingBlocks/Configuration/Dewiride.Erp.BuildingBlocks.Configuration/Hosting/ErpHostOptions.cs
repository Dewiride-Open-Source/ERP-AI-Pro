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
}
