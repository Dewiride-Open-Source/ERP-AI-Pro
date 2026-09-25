namespace Dewiride.Erp.BuildingBlocks.Endpoints.Security;

public sealed class AllowedHostsOptions
{
    public const string AnyHost = "*";

    public IReadOnlyList<string> Hosts { get; set; } = [AnyHost];
}
