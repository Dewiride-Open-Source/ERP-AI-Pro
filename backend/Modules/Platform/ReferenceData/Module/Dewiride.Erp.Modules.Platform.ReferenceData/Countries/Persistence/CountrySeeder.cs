using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Persistence;

// The table follows the file: a missing code is added, a name follows the file and a code the file does not list is
// deleted, each change recorded. No person changes a country, so following the file overwrites nobody's change, and other
// modules keep a country's code rather than its row, so a deleted code leaves their records whole.
internal sealed class CountrySeeder(ReferenceDataDbContext context) : ISeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var file = CountrySourceFile.Read();
        var stored = await context.Countries.ToDictionaryAsync(c => c.Code, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        foreach (var entry in file.Countries)
        {
            if (stored.Remove(entry.Code, out var country))
            {
                country.Rename(entry.Name);
            }
            else
            {
                context.Countries.Add(Country.Create(entry.Code, entry.Name));
            }
        }

        context.Countries.RemoveRange(stored.Values);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
