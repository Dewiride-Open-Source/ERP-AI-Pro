namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Entries;

public enum TdsTiming
{
    CreditOrPayment = 1,
    Payment = 2,
    LastMonth = 3,
    BeforeDistribution = 4,
    WithdrawalOrYearEnd = 5,
    Deferred = 6,
}
