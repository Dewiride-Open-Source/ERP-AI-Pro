namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Companies.ScheduleII;

public sealed record ScheduleIILimits(StatutoryParameterVersion Version, IReadOnlyList<ScheduleIILimit> Limits) : IStatutoryParameterSet<ScheduleIILimits>
{
    public static string Key => "companies.schedule-ii-limits";

    public static ScheduleIILimits Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ScheduleIILimit>());
}
