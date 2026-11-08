namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;

public sealed record EsiAverageDailyWageDivisors(StatutoryParameterVersion Version, IReadOnlyList<EsiAverageDailyWageDivisor> Divisors) : IStatutoryParameterSet<EsiAverageDailyWageDivisors>
{
    public static string Key => "esi.average-daily-wage-divisors";

    public static EsiAverageDailyWageDivisors Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<EsiAverageDailyWageDivisor>());
}
