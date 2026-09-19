namespace Dewiride.Erp.BuildingBlocks.Modules;

public sealed class ModuleCatalog
{
    public ModuleCatalog(IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var duplicate = modules.GroupBy(m => m.Descriptor.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Module '{duplicate.Key}' is registered more than once.");
        }

        Modules = modules;
    }

    public IReadOnlyList<IModule> Modules { get; }
}
