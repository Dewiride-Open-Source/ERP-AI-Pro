namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;

// Section 34(c) of the Code on Wages, 2019 leaves these sums to rules of the Central Government alone (section 67(3)(f)), so they
// apply whatever the appropriate Government.
public sealed record BonusPriorCharges(StatutoryParameterVersion Version, IReadOnlyList<BonusPriorCharge> Charges) : IStatutoryParameterSet<BonusPriorCharges>
{
    public static string Key => "bonus.prior-charges";

    public static BonusPriorCharges Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version, rows.Read<BonusPriorCharge>());
}
