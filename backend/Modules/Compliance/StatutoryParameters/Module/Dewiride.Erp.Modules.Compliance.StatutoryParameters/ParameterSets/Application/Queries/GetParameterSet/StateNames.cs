using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Application.Queries.GetParameterSet;

// A GST State code can stand for different territories over time, so a code is named as the reference data names it on the
// day asked, which for an entry is its first day: the seed files hold every code of an entry in force on all of its days.
internal sealed class StateNames(IReadOnlyList<IndianStateEntry> states)
{
    public static StateNames None { get; } = new([]);

    public string? NameOf(string gstCode, DateOnly on) =>
        states.FirstOrDefault(state =>
            string.Equals(state.GstCode, gstCode, StringComparison.Ordinal)
            && state.ValidFrom <= on
            && (state.ValidUntil is not { } until || on <= until))?.Name;
}
