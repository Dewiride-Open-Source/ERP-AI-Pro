using Dewiride.Erp.ArchitectureTests.Rules.Probes.Persistence;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dewiride.Erp.ArchitectureTests.Rules;

// A context that numbers its documents maps the numbering's two tables as their configurations do, so that its numbers and the
// numbering's own changes meet in the same rows, and leaves them out of its migrations, which only NumberingDbContext's create.
public sealed class NumberingTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly (Type Type, string Table)[] Tables = [(typeof(NumberSeries), NumberingTables.Series), (typeof(NumberSeriesYear), NumberingTables.Years)];

    [Fact]
    public void CatalogueContexts_MapTheNumberingTablesAsTheirOwnerKeepsThem()
    {
        using var scope = factory.Services.CreateScope();
        var registrations = factory.Services.GetRequiredService<DbContextCatalog>().Registrations;

        var problems = registrations
            .SelectMany(registration => TableProblems(DesignTimeModelOf((DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType)), registration.ContextType))
            .ToList();

        Assert.Empty(problems);
        Assert.Contains(registrations, registration => registration.ContextType == typeof(NumberingDbContext));
    }

    [Fact]
    public void TableProblems_AContextMappingTheTablesAsConfigured_HasNone()
    {
        using var context = new ProbeContexts.Numbered();

        Assert.Empty(TableProblems(DesignTimeModelOf(context), typeof(ProbeContexts.Numbered)));
    }

    [Fact]
    public void TableProblems_AContextThatRenamesATable_IsReported()
    {
        using var context = new ProbeContexts.Renumbered();

        Assert.Equal([$"{nameof(ProbeContexts.Renumbered)} maps {nameof(NumberSeriesYear)} to probe_renumbered.Years, not {NumberingTables.Schema}.{NumberingTables.Years}"], TableProblems(DesignTimeModelOf(context), typeof(ProbeContexts.Renumbered)));
    }

    [Fact]
    public void TableProblems_AContextThatWouldCreateATable_IsReported()
    {
        using var context = new ProbeContexts.Migrating();

        Assert.Equal([$"{nameof(ProbeContexts.Migrating)} creates {NumberingTables.Schema}.{NumberingTables.Series}, which only {nameof(NumberingDbContext)}'s migrations create"], TableProblems(DesignTimeModelOf(context), typeof(ProbeContexts.Migrating)));
    }

    // Whether a table is left out of the migrations is kept in the design-time model alone.
    private static IModel DesignTimeModelOf(DbContext context) => context.GetService<IDesignTimeModel>().Model;

    private static List<string> TableProblems(IModel model, Type contextType)
    {
        var problems = new List<string>();
        foreach (var (type, table) in Tables)
        {
            if (model.FindEntityType(type) is not { } entity)
            {
                continue;
            }

            if ((entity.GetSchema(), entity.GetTableName()) != (NumberingTables.Schema, table))
            {
                problems.Add($"{contextType.Name} maps {type.Name} to {entity.GetSchema()}.{entity.GetTableName()}, not {NumberingTables.Schema}.{table}");
            }
            else if (entity.IsTableExcludedFromMigrations() == (contextType == typeof(NumberingDbContext)))
            {
                problems.Add(contextType == typeof(NumberingDbContext)
                    ? $"{contextType.Name} leaves {NumberingTables.Schema}.{table} out of its migrations, which create it"
                    : $"{contextType.Name} creates {NumberingTables.Schema}.{table}, which only {nameof(NumberingDbContext)}'s migrations create");
            }
        }

        return problems;
    }
}
