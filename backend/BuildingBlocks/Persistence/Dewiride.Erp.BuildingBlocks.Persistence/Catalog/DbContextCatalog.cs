namespace Dewiride.Erp.BuildingBlocks.Persistence.Catalog;

public sealed class DbContextCatalog
{
    public DbContextCatalog(IEnumerable<DbContextRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var list = registrations.ToList();
        var duplicateType = list.GroupBy(r => r.ContextType).FirstOrDefault(g => g.Count() > 1);
        if (duplicateType is not null)
        {
            throw new InvalidOperationException($"DbContext {duplicateType.Key.Name} is registered more than once.");
        }

        var duplicateSchema = list.GroupBy(r => r.Schema, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicateSchema is not null)
        {
            throw new InvalidOperationException(
                $"Schema '{duplicateSchema.Key}' is claimed by {string.Join(" and ", duplicateSchema.Select(r => r.ContextType.Name))}; every DbContext owns its own schema.");
        }

        Registrations = list;
    }

    public IReadOnlyList<DbContextRegistration> Registrations { get; }
}
