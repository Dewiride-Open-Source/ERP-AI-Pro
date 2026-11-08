using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.AdvanceTax;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.IncomeTax;

// Golden values of advance tax for tax years from 2026-27: when it is payable, the share due by each instalment's date and the
// interest for not paying or for deferring it, each pinned beside its instrument so that a wrong edit of a seed file fails a test
// (ADR-0063). A due date's month is numbered within the tax year as the financial periods number the months of a financial year, so
// each date of the tax year 2026-27 is pinned as a calendar date too. The rows are standing law and stay in force.
[Collection(StatutoryRows.Name)]
public sealed class AdvanceTaxGoldenTests(ErpApiFactory factory)
{
    private const string InstalmentsKey = "income-tax.advance-tax-instalments";

    private const string InterestKey = "income-tax.advance-tax-interest";

    private static readonly DateOnly FirstTaxYear = new(2026, 4, 1);

    private static readonly DateOnly SecondTaxYear = new(2027, 4, 1);

    [Theory]
    [InlineData("liability-limit", null, null, null, null, "10000", "where the amount of such tax payable during that year, as computed under this Part, is ₹10000 or more.", "Income-tax Act, 2025, sections 403(1) and (2) and 404")]
    [InlineData("instalment-1", AdvanceTaxPayers.General, 15, 3, "15", null, "1. On or before the 15th June. Not less than 15% of such advance tax.", "Income-tax Act, 2025, section 408(1), Table, Sl. No. 1")]
    [InlineData("instalment-2", AdvanceTaxPayers.General, 15, 6, "45", null, "2. On or before the 15th September. Not less than 45% of such advance tax, as reduced by the amount, if any, paid in the earlier instalment.", "Income-tax Act, 2025, section 408(1), Table, Sl. No. 2")]
    [InlineData("instalment-3", AdvanceTaxPayers.General, 15, 9, "75", null, "3. On or before the 15th December. Not less than 75% of such advance tax, as reduced by the amount or amounts, if any, paid in the earlier instalment or instalments.", "Income-tax Act, 2025, section 408(1), Table, Sl. No. 3")]
    [InlineData("instalment-4", AdvanceTaxPayers.General, 15, 12, "100", null, "4. On or before the 15th March. The whole amount of such advance tax, as reduced by the amount or amounts, if any, paid in the earlier instalment or instalments.", "Income-tax Act, 2025, section 408(1), Table, Sl. No. 4")]
    [InlineData("presumptive", AdvanceTaxPayers.Presumptive, 15, 12, "100", null, "shall pay the whole amount of advance tax on the current income, calculated in the manner laid down in section 405 during each financial year, on or before the 15th March.", "Income-tax Act, 2025, section 408(2)")]
    [InlineData("paid-by-31-march", null, 31, 12, null, null, "(3) Any amount paid by way of advance tax on or before the 31st March, shall be treated as advance tax paid during the financial year ending on that day", "Income-tax Act, 2025, section 408(3)")]
    public async Task Rule_OfAdvanceTaxInTheFirstTaxYear_IsAsItsInstrumentWritesIt(string rule, AdvanceTaxPayers? payers, int? dueDay, int? taxYearMonth, string? cumulativePercent, string? amount, string words, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rules = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<AdvanceTaxInstalments>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rules;

        var found = Assert.Single(rules, candidate => candidate.Rule == rule);
        Assert.Equal(payers, found.Payers);
        Assert.Equal(dueDay, found.DueDay);
        Assert.Equal(taxYearMonth, found.TaxYearMonth);
        Assert.Equal(Percent(cumulativePercent), found.CumulativePercent);
        Assert.Equal(Rupees(amount), found.Amount);
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, InstalmentsKey, rule), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("default", null, null, "90", AdvanceTaxShareOf.AssessedTax, null, "1", AdvanceTaxInterestBasis.EveryMonthOrPart, "is less than 90% of the assessed tax, the assessee shall be liable to pay simple interest at the rate of 1% for every month or part of a month", "Income-tax Act, 2025, section 424(1)")]
    [InlineData("deferment-1", 15, 3, "15", AdvanceTaxShareOf.TaxDueOnReturnedIncome, "12", "3", AdvanceTaxInterestBasis.Once, "1. 15th day of June. 15% of the tax due on returned income. Shortfall till 15th day of June. 3%", "Income-tax Act, 2025, section 425(1), Table, Sl. No. 1, and section 425(2)(a)")]
    [InlineData("deferment-2", 15, 6, "45", AdvanceTaxShareOf.TaxDueOnReturnedIncome, "36", "3", AdvanceTaxInterestBasis.Once, "2. 15th day of September. 45% of the tax due on returned income. Shortfall till 15th day of September. 3%", "Income-tax Act, 2025, section 425(1), Table, Sl. No. 2, and section 425(2)(b)")]
    [InlineData("deferment-3", 15, 9, "75", AdvanceTaxShareOf.TaxDueOnReturnedIncome, null, "3", AdvanceTaxInterestBasis.Once, "3. 15th day of December. 75% of the tax due on returned income. Shortfall till 15th day of December. 3%", "Income-tax Act, 2025, section 425(1), Table, Sl. No. 3")]
    [InlineData("deferment-4", 15, 12, "100", AdvanceTaxShareOf.TaxDueOnReturnedIncome, null, "1", AdvanceTaxInterestBasis.Once, "4. 15th day of March. 100% of the tax due on returned income. Shortfall till 15th day of March. 1%", "Income-tax Act, 2025, section 425(1), Table, Sl. No. 4")]
    [InlineData("deferment-presumptive", 15, 12, "100", AdvanceTaxShareOf.TaxDueOnReturnedIncome, null, "1", AdvanceTaxInterestBasis.Once, "is less than the tax due on returned income, shall be liable to pay simple interest at the rate of 1% on the amount of shortfall", "Income-tax Act, 2025, section 425(3)")]
    [InlineData("deferment-exclusions", null, null, null, null, null, null, null, "No interest shall be payable under sub-section (1) or (3) in respect of shortfall in the payment of tax due on returned income", "Income-tax Act, 2025, section 425(4)")]
    public async Task Rule_OfInterestOnAdvanceTaxInTheFirstTaxYear_IsAsItsInstrumentWritesIt(string rule, int? dueDay, int? taxYearMonth, string? share, AdvanceTaxShareOf? shareOf, string? noInterestAtOrAbove, string? interest, AdvanceTaxInterestBasis? interestBasis, string words, string instrument)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var rules = (await scope.ServiceProvider.GetRequiredService<IStatutoryParameters>().GetAsOfAsync<AdvanceTaxInterest>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rules;

        var found = Assert.Single(rules, candidate => candidate.Rule == rule);
        Assert.Equal(dueDay, found.DueDay);
        Assert.Equal(taxYearMonth, found.TaxYearMonth);
        Assert.Equal(Percent(share), found.Share);
        Assert.Equal(shareOf, found.ShareOf);
        Assert.Equal(Percent(noInterestAtOrAbove), found.NoInterestAtOrAbove);
        Assert.Equal(Percent(interest), found.Interest);
        Assert.Equal(interestBasis, found.InterestBasis);
        Assert.Contains(words, found.Wording, StringComparison.Ordinal);
        Assert.Contains(instrument, await SourceAsync(scope, InterestKey, rule), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstalmentsKey, "instalment-1", "2026-06-15")]
    [InlineData(InstalmentsKey, "instalment-2", "2026-09-15")]
    [InlineData(InstalmentsKey, "instalment-3", "2026-12-15")]
    [InlineData(InstalmentsKey, "instalment-4", "2027-03-15")]
    [InlineData(InstalmentsKey, "presumptive", "2027-03-15")]
    [InlineData(InstalmentsKey, "paid-by-31-march", "2027-03-31")]
    [InlineData(InterestKey, "deferment-1", "2026-06-15")]
    [InlineData(InterestKey, "deferment-2", "2026-09-15")]
    [InlineData(InterestKey, "deferment-3", "2026-12-15")]
    [InlineData(InterestKey, "deferment-4", "2027-03-15")]
    [InlineData(InterestKey, "deferment-presumptive", "2027-03-15")]
    public async Task DueDate_OfEachRuleInTheTaxYear2026To27_IsTheDayTheActNames(string key, string rule, string date)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var (taxYearMonth, dueDay) = await DueDateOfAsync(scope.ServiceProvider.GetRequiredService<IStatutoryParameters>(), key, rule);

        var period = new FinancialPeriod(FinancialYear.FromDate(FirstTaxYear), taxYearMonth!.Value);
        Assert.Equal(DateOnly.Parse(date, CultureInfo.InvariantCulture), period.Start.AddDays(dueDay!.Value - 1));
    }

    [Theory]
    [InlineData(InstalmentsKey, 7, "Income-tax Act, 2025 (No. 30 of 2025), 21 August 2025, sections 403, 404, 405 and 408")]
    [InlineData(InterestKey, 7, "Income-tax Act, 2025 (No. 30 of 2025), 21 August 2025, sections 424 and 425|Finance Act, 2026 (No. 4 of 2026), 30 March 2026, sections 94 and 95")]
    public async Task Version_OfEachSet_IsTheOneThatBroughtTheIncomeTaxAct2025AndAddsEveryRule(string key, int added, string instruments)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var set = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetSetAsync(key, FirstTaxYear, TestContext.Current.CancellationToken);

        var version = Assert.Single(set!.Versions);
        Assert.Equal(FirstTaxYear, version.From);
        Assert.Null(version.Until);
        var cited = instruments.Split('|');
        Assert.Equal(cited.Length, version.Instruments.Count);
        Assert.All(cited.Zip(version.Instruments), pair => Assert.StartsWith(pair.First, pair.Second, StringComparison.Ordinal));
        Assert.Equal((added, 0, 0), (version.AddedCount, version.ChangedCount, version.RemovedCount));
    }

    [Fact]
    public async Task Rules_InTheSecondTaxYear_AreTheStandingLawOfTheFirst()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var instalments = await store.GetAsOfAsync<AdvanceTaxInstalments>(SecondTaxYear, TestContext.Current.CancellationToken);
        var interest = await store.GetAsOfAsync<AdvanceTaxInterest>(SecondTaxYear, TestContext.Current.CancellationToken);

        Assert.NotNull(instalments);
        Assert.Equal(FirstTaxYear, instalments.Version.From);
        Assert.Equal(7, instalments.Rules.Count);
        Assert.NotNull(interest);
        Assert.Equal(FirstTaxYear, interest.Version.From);
        Assert.Equal(7, interest.Rules.Count);
    }

    [Fact]
    public async Task AdvanceTaxSets_OnTheDayBeforeTheIncomeTaxAct2025CameIntoForce_HoldNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var day = FirstTaxYear.AddDays(-1);

        Assert.Null(await store.GetAsOfAsync<AdvanceTaxInstalments>(day, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetAsOfAsync<AdvanceTaxInterest>(day, TestContext.Current.CancellationToken));
    }

    private static Percentage? Percent(string? value) => value is null ? null : new Percentage(decimal.Parse(value, CultureInfo.InvariantCulture));

    private static Money? Rupees(string? value) => value is null ? null : new Money(decimal.Parse(value, CultureInfo.InvariantCulture), Currency.Inr);

    private static async Task<(int? TaxYearMonth, int? DueDay)> DueDateOfAsync(IStatutoryParameters store, string key, string rule)
    {
        if (key == InstalmentsKey)
        {
            var instalment = Assert.Single((await store.GetAsOfAsync<AdvanceTaxInstalments>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rules, candidate => candidate.Rule == rule);

            return (instalment.TaxYearMonth, instalment.DueDay);
        }

        var deferment = Assert.Single((await store.GetAsOfAsync<AdvanceTaxInterest>(FirstTaxYear, TestContext.Current.CancellationToken))!.Rules, candidate => candidate.Rule == rule);

        return (deferment.TaxYearMonth, deferment.DueDay);
    }

    private static async Task<string> SourceAsync(AsyncServiceScope scope, string key, string rowKey)
    {
        var view = await scope.ServiceProvider.GetRequiredService<IStatutoryParameterHistory>().GetVersionAsync(key, FirstTaxYear, TestContext.Current.CancellationToken);

        return Assert.Single(view!.Rows, row => row.Key == rowKey).Source;
    }
}
