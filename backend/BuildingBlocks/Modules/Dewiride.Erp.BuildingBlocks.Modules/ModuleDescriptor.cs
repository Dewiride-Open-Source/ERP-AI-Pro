namespace Dewiride.Erp.BuildingBlocks.Modules;

public sealed record ModuleDescriptor(string Domain, string Name, string? Schema, string RoutePrefix, string FeatureFlag, IReadOnlyCollection<string> Permissions)
{
    public string Id => $"{Domain}.{Name}";
}
