using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.PublicApi;

// A typed set whose key no seed file holds is a defect of the release, which a test of every typed set rules out, so it throws
// rather than reading as a date the ERP holds nothing for.
internal sealed class StatutoryParameterStore(StatutoryParametersDbContext context) : IStatutoryParameters
{
    public async Task<TSet?> GetAsOfAsync<TSet>(DateOnly date, CancellationToken cancellationToken)
        where TSet : class, IStatutoryParameterSet<TSet>
    {
        var key = TSet.Key;
        var set = await context.ParameterSets.AsNoTracking().SingleOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No statutory parameter set is seeded under the key {key} that {typeof(TSet).Name} reads.");
        var version = await context.ParameterVersions
            .AsNoTracking()
            .Where(v => v.SetId == set.Id && v.From <= date)
            .OrderByDescending(v => v.From)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (version is null)
        {
            return null;
        }

        var next = await context.ParameterVersions
            .Where(v => v.SetId == set.Id && v.From > date)
            .OrderBy(v => v.From)
            .Select(v => (DateOnly?)v.From)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var rows = await context.ParameterRows
            .AsNoTracking()
            .Where(r => r.SetId == set.Id && r.From <= date && (r.Until == null || r.Until >= date))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return TSet.Create(
            new StatutoryParameterVersion(version.From, next?.AddDays(-1), version.Instruments),
            new TypedParameterRows(set, [.. rows.OrderBy(row => row.RowKey, StringComparer.Ordinal)]));
    }
}
