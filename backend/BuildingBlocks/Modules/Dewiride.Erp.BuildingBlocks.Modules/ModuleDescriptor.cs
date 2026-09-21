namespace Dewiride.Erp.BuildingBlocks.Modules;

public sealed record ModuleDescriptor(
    string Domain,
    string Name,
    string? Schema,
    string RoutePrefix,
    string FeatureFlag,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<ModuleCapability> Capabilities)
{
    public string Id => $"{Domain}.{Name}";

    public string PermissionPrefix => RoutePrefix.Trim('/').Replace('/', '.') + ".";
}
