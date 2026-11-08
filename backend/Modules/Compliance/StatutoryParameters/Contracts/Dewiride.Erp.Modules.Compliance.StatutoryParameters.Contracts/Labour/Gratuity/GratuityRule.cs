using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Gratuity;

// Bound says how the rule's figure binds: its count of units where it has one, otherwise its percent or amount. A rule without a
// figure is in its words alone.
public sealed record GratuityRule(
    string Provision,
    string Name,
    GratuitySphere Sphere,
    int? Count,
    GratuityUnit? Unit,
    GratuityBound? Bound,
    Percentage? Percent,
    Money? Amount,
    string Wording);
