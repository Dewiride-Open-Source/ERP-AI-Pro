namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Contributions;

public sealed record TenPerCentEstablishments(StatutoryParameterVersion Version, IReadOnlyList<TenPerCentEstablishment> Entries) : IStatutoryParameterSet<TenPerCentEstablishments>
{
    public static string Key => "provident-fund.ten-per-cent-establishments";

    public static TenPerCentEstablishments Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<TenPerCentEstablishment>());
}
