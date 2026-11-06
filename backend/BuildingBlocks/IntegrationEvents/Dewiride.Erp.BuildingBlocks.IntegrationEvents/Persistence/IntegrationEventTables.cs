using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;

// The catalogue contexts that map each table, read once from their models, because a module opts in by declaring the table's
// DbSet and adding its migration.
internal sealed class IntegrationEventTables(IServiceScopeFactory scopes, DbContextCatalog catalog)
{
    private readonly Lazy<MappedTables> _mapped = new(() => Read(scopes, catalog));

    public IReadOnlyList<Type> Outboxes => _mapped.Value.Outboxes;

    public IReadOnlyList<Type> ProcessedRecords => _mapped.Value.ProcessedRecords;

    private static MappedTables Read(IServiceScopeFactory scopes, DbContextCatalog catalog)
    {
        using var scope = scopes.CreateScope();
        var outboxes = new List<Type>();
        var processedRecords = new List<Type>();
        foreach (var registration in catalog.Registrations)
        {
            var model = ((DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType)).Model;
            if (model.FindEntityType(typeof(OutboxMessage)) is not null)
            {
                outboxes.Add(registration.ContextType);
            }

            if (model.FindEntityType(typeof(ProcessedMessage)) is not null)
            {
                processedRecords.Add(registration.ContextType);
            }
        }

        return new MappedTables(outboxes, processedRecords);
    }

    private sealed record MappedTables(IReadOnlyList<Type> Outboxes, IReadOnlyList<Type> ProcessedRecords);
}
