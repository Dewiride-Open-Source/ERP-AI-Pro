namespace Dewiride.Erp.BuildingBlocks.Modules;

public sealed class ModuleCatalog
{
    public ModuleCatalog(IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var duplicate = modules.GroupBy(m => m.Descriptor.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Module '{duplicate.Key}' is registered more than once.");
        }

        RejectShared(modules, d => d.RoutePrefix, "route prefix");
        RejectShared(modules, d => d.Schema, "schema");
        RejectShared(modules, d => d.FeatureFlag, "feature flag");

        foreach (var descriptor in modules.Select(m => m.Descriptor))
        {
            var foreign = descriptor.Permissions.FirstOrDefault(p => !p.StartsWith(descriptor.PermissionPrefix, StringComparison.Ordinal));
            if (foreign is not null)
            {
                throw new InvalidOperationException($"Module '{descriptor.Id}' declares the permission '{foreign}', which must start with '{descriptor.PermissionPrefix}'.");
            }
        }

        Modules = modules;
    }

    public IReadOnlyList<IModule> Modules { get; }

    private static void RejectShared(IReadOnlyList<IModule> modules, Func<ModuleDescriptor, string?> value, string name)
    {
        var shared = modules
            .Select(m => (m.Descriptor.Id, Value: value(m.Descriptor)))
            .Where(x => x.Value is not null)
            .GroupBy(x => x.Value!, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (shared is not null)
        {
            var owners = string.Join(" and ", shared.Select(x => $"'{x.Id}'"));
            throw new InvalidOperationException($"Modules {owners} declare the same {name} '{shared.Key}'.");
        }
    }
}
