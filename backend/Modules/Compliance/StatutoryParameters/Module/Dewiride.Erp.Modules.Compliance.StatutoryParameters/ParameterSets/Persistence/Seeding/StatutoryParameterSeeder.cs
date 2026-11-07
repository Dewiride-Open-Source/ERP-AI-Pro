using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

// The tables follow the files, which no person edits in the ERP: every file is read and checked before the seeder writes
// anything, then a set is known by its key, a version by its set and date and a row by its set, key and start, a missing one is
// added, the rest of one follows its file and one no file holds is deleted, rows and versions before their set, each change
// recorded, all in one save. A corrected date is therefore a deletion and an addition. It runs after the GST State codes are
// seeded, because a row's State code must be in force on every day of the row.
internal sealed class StatutoryParameterSeeder(StatutoryParametersDbContext context, IIndianStates indianStates) : ISeeder
{
    public Task SeedAsync(CancellationToken cancellationToken) => SeedAsync(StatutorySeedFiles.Embedded(), cancellationToken);

    internal async Task SeedAsync(IReadOnlyList<StatutorySeedSource> sources, CancellationToken cancellationToken)
    {
        var states = await indianStates.ListAsync(null, cancellationToken).ConfigureAwait(false);
        var seeded = StatutorySeedFiles.Read(sources, states);

        var sets = await context.ParameterSets.ToDictionaryAsync(set => set.Key, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        var versions = (await context.ParameterVersions.ToListAsync(cancellationToken).ConfigureAwait(false)).ToLookup(version => version.SetId);
        var rows = (await context.ParameterRows.ToListAsync(cancellationToken).ConfigureAwait(false)).ToLookup(row => row.SetId);
        foreach (var file in seeded)
        {
            if (sets.Remove(file.Set.Key, out var stored))
            {
                stored.Revise(file.Set.Title, file.Set.Description, file.Set.Basis, file.Set.AsOf, file.Set.Columns);
                Follow(stored, file, versions[stored.Id], rows[stored.Id]);
            }
            else
            {
                context.ParameterSets.Add(file.Set);
                context.ParameterVersions.AddRange(file.Versions);
                context.ParameterRows.AddRange(file.Rows);
            }
        }

        foreach (var removed in sets.Values)
        {
            context.ParameterRows.RemoveRange(rows[removed.Id]);
            context.ParameterVersions.RemoveRange(versions[removed.Id]);
            context.ParameterSets.Remove(removed);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void Follow(ParameterSet set, SeededSet file, IEnumerable<ParameterVersion> storedVersions, IEnumerable<ParameterRow> storedRows)
    {
        var versions = storedVersions.ToDictionary(version => version.From);
        foreach (var version in file.Versions)
        {
            if (versions.Remove(version.From, out var stored))
            {
                stored.Revise(version.Instruments);
            }
            else
            {
                context.ParameterVersions.Add(ParameterVersion.Create(set, version.From, version.Instruments));
            }
        }

        context.ParameterVersions.RemoveRange(versions.Values);

        var rows = storedRows.ToDictionary(row => (row.RowKey, row.From));
        foreach (var row in file.Rows)
        {
            if (rows.Remove((row.RowKey, row.From), out var stored))
            {
                stored.Revise(set, row.Until, row.Cells, row.Source);
            }
            else
            {
                context.ParameterRows.Add(ParameterRow.Create(set, row.From, row.Until, row.Cells, row.Source));
            }
        }

        context.ParameterRows.RemoveRange(rows.Values);
    }
}
