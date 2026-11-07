namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;

// The GST State codes the two lists of rule 61(1)(ii) name; a code in neither list has no row, so no quarterly due date of
// GSTR-3B follows from this set for it.
public sealed record QuarterlyReturnStates(StatutoryParameterVersion Version, IReadOnlyList<QuarterlyReturnState> States) : IStatutoryParameterSet<QuarterlyReturnStates>
{
    public static string Key => "gst.quarterly-return-states";

    public static QuarterlyReturnStates Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<QuarterlyReturnState>());
}
