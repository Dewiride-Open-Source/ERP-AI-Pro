namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Contributions;

public sealed record ProvidentFundMembershipLimits(StatutoryParameterVersion Version, IReadOnlyList<ProvidentFundMembershipLimit> Limits) : IStatutoryParameterSet<ProvidentFundMembershipLimits>
{
    public static string Key => "provident-fund.membership-limits";

    public static ProvidentFundMembershipLimits Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<ProvidentFundMembershipLimit>());
}
