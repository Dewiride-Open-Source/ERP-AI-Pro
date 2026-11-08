namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.WelfareFund;

// Day is the first such day after RegisterDay; without a register day, the instrument's words place it in the year.
public sealed record WelfareFundDueDay(
    string Entry,
    string StateCode,
    string Name,
    WelfareFundObligation Obligation,
    string? Form,
    WelfareFundDueBound Bound,
    DayAndMonth Day,
    DayAndMonth? RegisterDay,
    string Wording);
