using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Remittance;

// Rate is a per cent of the pay or wages on which contributions are payable, or would be but for an exemption, and the charge is
// never less than Minimum.
public sealed record ProvidentFundCharge(
    string Charge,
    string Name,
    ChargeScheme Scheme,
    ChargedEstablishments Establishments,
    Percentage Rate,
    Money Minimum,
    string Wording);
