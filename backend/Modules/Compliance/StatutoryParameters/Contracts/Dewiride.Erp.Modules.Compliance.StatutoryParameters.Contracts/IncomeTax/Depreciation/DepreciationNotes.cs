namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Depreciation;

public sealed record DepreciationNotes(StatutoryParameterVersion Version, IReadOnlyList<DepreciationNote> Notes) : IStatutoryParameterSet<DepreciationNotes>
{
    public static string Key => "income-tax.depreciation-notes";

    public static DepreciationNotes Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<DepreciationNote>());
}
