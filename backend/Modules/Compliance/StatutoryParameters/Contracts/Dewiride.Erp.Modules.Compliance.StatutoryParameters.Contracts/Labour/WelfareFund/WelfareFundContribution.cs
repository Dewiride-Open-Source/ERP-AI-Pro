using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.WelfareFund;

// Every share is for one contribution period of Period: an amount, or the multiple of the employee's contribution the instrument
// states; the employee's is a percentage of pay up to EmployeeLimit where it has no amount, and the Government's is empty where
// the instrument names no share.
public sealed record WelfareFundContribution(
    string Entry,
    string StateCode,
    string Act,
    WelfareFundPeriod Period,
    Money? EmployeeAmount,
    Percentage? EmployeePercent,
    Money? EmployeeLimit,
    Money? EmployerAmount,
    int? EmployerTimesEmployee,
    Money? GovernmentAmount,
    int? GovernmentTimesEmployee,
    string Wording,
    string? Deduction);
