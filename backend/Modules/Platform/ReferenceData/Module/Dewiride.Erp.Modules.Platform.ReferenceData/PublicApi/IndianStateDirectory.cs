using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.PublicApi;

internal sealed class IndianStateDirectory(ReferenceDataDbContext context) : IIndianStates
{
    public async Task<IReadOnlyList<IndianStateEntry>> ListAsync(DateOnly? on, CancellationToken cancellationToken)
    {
        var states = context.IndianStates.AsNoTracking();
        if (on is { } date)
        {
            states = states.Where(s => s.ValidFrom <= date && (s.ValidUntil == null || s.ValidUntil >= date));
        }

        var rows = await states
            .OrderBy(s => s.GstCode)
            .ThenBy(s => s.ValidFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(Entry)];
    }

    public async Task<IndianStateEntry?> FindAsync(string gstCode, DateOnly on, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gstCode);
        if (!IndianState.IsGstCode(gstCode))
        {
            return null;
        }

        var state = await context.IndianStates
            .AsNoTracking()
            .Where(s => s.GstCode == gstCode && s.ValidFrom <= on && (s.ValidUntil == null || s.ValidUntil >= on))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return state is null ? null : Entry(state);
    }

    private static IndianStateEntry Entry(IndianState state) =>
        new(state.GstCode, state.Name, KindOf(state.Kind), state.ValidFrom, state.ValidUntil);

    private static IndianStateKind KindOf(TerritoryKind kind) => kind switch
    {
        TerritoryKind.State => IndianStateKind.State,
        TerritoryKind.UnionTerritory => IndianStateKind.UnionTerritory,
        TerritoryKind.OtherTerritory => IndianStateKind.OtherTerritory,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A State code is a State, a Union Territory or the Other Territory."),
    };
}
