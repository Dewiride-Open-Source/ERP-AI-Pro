using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Seeding;

public sealed class SampleSeeder(SampleDbContext context, SampleSeedNames names, TimeProvider clock) : ISeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var existing = await context.Samples.IncludeDeleted()
            .Where(sample => names.Names.Contains(sample.Name))
            .Select(sample => sample.Name)
            .ToListAsync(cancellationToken);

        foreach (var name in names.Names.Except(existing, StringComparer.Ordinal))
        {
            context.Samples.Add(new SampleAggregate(SampleId.Create(), name, new Money(1m, Currency.Inr), new SampleAddress("1 Seed Street", "Pune"), clock.GetUtcNow()));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
