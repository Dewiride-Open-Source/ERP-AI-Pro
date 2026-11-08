namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;

public sealed record TdsTerms(StatutoryParameterVersion Version, IReadOnlyList<TdsTerm> Terms) : IStatutoryParameterSet<TdsTerms>
{
    public static string Key => "income-tax.tds-terms";

    public static TdsTerms Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsTerm>());
}
