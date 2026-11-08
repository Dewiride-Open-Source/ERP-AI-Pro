using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.WelfareFund;

// A supervisor whose wages for a month exceed SupervisorWageLimit is left out; every other test of the Act, the number of persons
// that brings an establishment in among them, is in its words.
public sealed record WelfareFundCoverageDefinition(
    string Entry,
    string StateCode,
    string Act,
    string Establishments,
    string? Employees,
    string? Excluded,
    Money? SupervisorWageLimit,
    int? MinimumDaysEmployed);
