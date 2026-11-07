using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Persistence;

// The table follows the file, which never lets two rows of a code overlap: a row is known by its code and start date, a
// missing one is added, the rest of a row follows the file and a row the file does not list is deleted, each change
// recorded, so a corrected start date replaces its row instead of overlapping it. No person changes a row.
internal sealed class IndianStateSeeder(ReferenceDataDbContext context) : ISeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var file = IndianStateSourceFile.Read();
        var stored = await context.IndianStates.ToDictionaryAsync(s => (s.GstCode, s.ValidFrom), cancellationToken).ConfigureAwait(false);
        foreach (var entry in file.States)
        {
            if (stored.Remove((entry.GstCode, entry.ValidFrom), out var state))
            {
                state.Revise(entry.Name, entry.Kind, entry.ValidUntil, entry.Source);
            }
            else
            {
                context.IndianStates.Add(IndianState.Create(entry.GstCode, entry.ValidFrom, entry.Name, entry.Kind, entry.ValidUntil, entry.Source));
            }
        }

        context.IndianStates.RemoveRange(stored.Values);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
