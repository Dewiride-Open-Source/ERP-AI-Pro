using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Codes;

public sealed record WageDefinitionClause(
    string Clause,
    string Name,
    LabourCode LabourCode,
    WageTreatment Treatment,
    Percentage? Percent,
    WageDefinitionBound? Bound,
    string Wording);
