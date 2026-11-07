namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;

// A due date is the due day of the month that falls MonthsAfter months after the tax period's last month, so 1 is the month
// that follows it and 9 is December after a financial year; OpensDay is the first day of that month a filing is accepted,
// null when the instrument names none.
public sealed record GstReturnDueDate(
    string Rule,
    string Name,
    string Form,
    ReturnFilers Filers,
    ReturnPeriod Period,
    int? OpensDay,
    int DueDay,
    int MonthsAfter,
    string Wording);
