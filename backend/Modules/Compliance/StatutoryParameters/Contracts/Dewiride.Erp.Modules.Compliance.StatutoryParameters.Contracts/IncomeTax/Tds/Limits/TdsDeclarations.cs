namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;

public sealed record TdsDeclarations(StatutoryParameterVersion Version, IReadOnlyList<TdsDeclaration> Declarations) : IStatutoryParameterSet<TdsDeclarations>
{
    public static string Key => "income-tax.tds-declarations";

    public static TdsDeclarations Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TdsDeclaration>());
}
