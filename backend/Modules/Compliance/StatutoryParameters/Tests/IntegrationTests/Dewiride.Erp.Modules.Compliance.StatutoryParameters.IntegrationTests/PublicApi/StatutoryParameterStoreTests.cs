using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Companies.ScheduleII;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Returns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Gst.Services;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.AdvanceTax;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Company;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Depreciation;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Crosswalk;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Entries;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.FinanceAct;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Limits;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.IncomeTax.Tds.Rates;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Bonus;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Codes;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Esi;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.Gratuity;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProfessionalTax;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Contributions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.ProvidentFund.Remittance;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Labour.WelfareFund;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.PublicApi;

// Each typed set is read on every one of its versions' dates and on the day before its first, the dates taken from its file.
[Collection(StatutoryRows.Name)]
public sealed class StatutoryParameterStoreTests(ErpApiFactory factory)
{
    public static TheoryData<string> Keys() => [.. SeedFileSets.All.Select(file => file.Set.Key)];

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task GetAsOfAsync_DayBeforeTheFirstVersion_AnswersNothing(string key)
    {
        var first = SeedFileSets.All.Single(file => file.Set.Key == key).Versions.Min(version => version.From);

        Assert.Null(await ReadAsync(key, first.AddDays(-1)));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task GetAsOfAsync_EachVersionsDate_AnswersThatVersionWithTheFilesRowsOfThatDay(string key)
    {
        var file = SeedFileSets.All.Single(seeded => seeded.Set.Key == key);
        var dates = file.Versions.Select(version => version.From).Order().ToList();

        for (var index = 0; index < dates.Count; index++)
        {
            var read = await ReadAsync(key, dates[index]);

            Assert.NotNull(read);
            var (version, keys) = read.Value;
            Assert.Equal(dates[index], version.From);
            Assert.Equal(index + 1 < dates.Count ? dates[index + 1].AddDays(-1) : null, version.Until);
            Assert.Equal(file.Versions.Single(declared => declared.From == dates[index]).Instruments, version.Instruments);
            Assert.Equal(file.Rows.Where(row => row.Span.Contains(dates[index])).Select(row => row.RowKey).Order(StringComparer.Ordinal), keys);
        }
    }

    [Fact]
    public async Task GetAsOfAsync_SetNoSeedFileHolds_Throws()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsOfAsync<UnseededSet>(new DateOnly(2021, 1, 1), TestContext.Current.CancellationToken));

        Assert.Contains("test.unseeded", refusal.Message, StringComparison.Ordinal);
    }

    private async Task<(StatutoryParameterVersion Version, IReadOnlyList<string> Keys)?> ReadAsync(string key, DateOnly day)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStatutoryParameters>();
        var cancellationToken = TestContext.Current.CancellationToken;

        return key switch
        {
            "gst.sac-codes" => Of(await store.GetAsOfAsync<SacCodes>(day, cancellationToken), set => (set.Version, [.. set.Codes.Select(code => code.Code)])),
            "gst.sac-rate-entries" => Of(await store.GetAsOfAsync<SacRateEntries>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(entry => entry.Code)])),
            "gst.central-tax-rates" => Of(await store.GetAsOfAsync<CentralTaxRates>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(entry => entry.Entry)])),
            "gst.integrated-tax-rates" => Of(await store.GetAsOfAsync<IntegratedTaxRates>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(entry => entry.Entry)])),
            "gst.union-territory-tax-rates" => Of(await store.GetAsOfAsync<UnionTerritoryTaxRates>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(entry => entry.Entry)])),
            "gst.return-due-dates" => Of(await store.GetAsOfAsync<GstReturnDueDates>(day, cancellationToken), set => (set.Version, [.. set.Rules.Select(rule => rule.Rule)])),
            "gst.quarterly-return-states" => Of(await store.GetAsOfAsync<QuarterlyReturnStates>(day, cancellationToken), set => (set.Version, [.. set.States.Select(state => state.StateCode)])),
            "gst.return-thresholds" => Of(await store.GetAsOfAsync<GstReturnThresholds>(day, cancellationToken), set => (set.Version, [.. set.Thresholds.Select(threshold => threshold.Threshold)])),
            "companies.schedule-ii-limits" => Of(await store.GetAsOfAsync<ScheduleIILimits>(day, cancellationToken), set => (set.Version, [.. set.Limits.Select(row => row.Limit)])),
            "companies.schedule-ii-useful-lives" => Of(await store.GetAsOfAsync<UsefulLives>(day, cancellationToken), set => (set.Version, [.. set.Lives.Select(row => row.Entry)])),
            "income-tax.advance-tax-instalments" => Of(await store.GetAsOfAsync<AdvanceTaxInstalments>(day, cancellationToken), set => (set.Version, [.. set.Rules.Select(row => row.Rule)])),
            "income-tax.advance-tax-interest" => Of(await store.GetAsOfAsync<AdvanceTaxInterest>(day, cancellationToken), set => (set.Version, [.. set.Rules.Select(row => row.Rule)])),
            "income-tax.company-rates" => Of(await store.GetAsOfAsync<CompanyTaxRates>(day, cancellationToken), set => (set.Version, [.. set.Rates.Select(row => row.Entry)])),
            "income-tax.company-surcharge" => Of(await store.GetAsOfAsync<CompanySurcharges>(day, cancellationToken), set => (set.Version, [.. set.Bands.Select(row => row.Band)])),
            "income-tax.depreciation-allowances" => Of(await store.GetAsOfAsync<DepreciationAllowances>(day, cancellationToken), set => (set.Version, [.. set.Allowances.Select(row => row.Allowance)])),
            "income-tax.depreciation-notes" => Of(await store.GetAsOfAsync<DepreciationNotes>(day, cancellationToken), set => (set.Version, [.. set.Notes.Select(row => row.Note)])),
            "income-tax.depreciation-rates" => Of(await store.GetAsOfAsync<DepreciationRates>(day, cancellationToken), set => (set.Version, [.. set.Rates.Select(row => row.Entry)])),
            "income-tax.health-and-education-cess" => Of(await store.GetAsOfAsync<HealthAndEducationCess>(day, cancellationToken), set => (set.Version, [.. set.Rates.Select(row => row.Cess)])),
            "income-tax.minimum-alternate-tax" => Of(await store.GetAsOfAsync<MinimumAlternateTax>(day, cancellationToken), set => (set.Version, [.. set.Rules.Select(row => row.Rule)])),
            "income-tax.tds-crosswalk" => Of(await store.GetAsOfAsync<TdsCrosswalk>(day, cancellationToken), set => (set.Version, [.. set.Pairs.Select(row => row.Pair)])),
            "income-tax.tds-declarations" => Of(await store.GetAsOfAsync<TdsDeclarations>(day, cancellationToken), set => (set.Version, [.. set.Declarations.Select(row => row.Row)])),
            "income-tax.tds-entries" => Of(await store.GetAsOfAsync<TdsEntries>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(row => row.Entry)])),
            "income-tax.tds-higher-rates" => Of(await store.GetAsOfAsync<TdsHigherRates>(day, cancellationToken), set => (set.Version, [.. set.Cases.Select(row => row.Case)])),
            "income-tax.tds-no-deduction-cases" => Of(await store.GetAsOfAsync<TdsNoDeductionCases>(day, cancellationToken), set => (set.Version, [.. set.Cases.Select(row => row.Case)])),
            "income-tax.tds-rates" => Of(await store.GetAsOfAsync<TdsRates>(day, cancellationToken), set => (set.Version, [.. set.Rates.Select(row => row.Rate)])),
            "income-tax.tds-rates-in-force" => Of(await store.GetAsOfAsync<TdsRatesInForce>(day, cancellationToken), set => (set.Version, [.. set.Rates.Select(row => row.Item)])),
            "income-tax.tds-rates-in-force-readings" => Of(await store.GetAsOfAsync<TdsRateInForceReadings>(day, cancellationToken), set => (set.Version, [.. set.Readings.Select(row => row.Reading)])),
            "income-tax.tds-surcharge-and-cess" => Of(await store.GetAsOfAsync<TdsSurchargesAndCess>(day, cancellationToken), set => (set.Version, [.. set.Clauses.Select(row => row.Clause)])),
            "income-tax.tds-terms" => Of(await store.GetAsOfAsync<TdsTerms>(day, cancellationToken), set => (set.Version, [.. set.Terms.Select(row => row.Term)])),
            "income-tax.tds-threshold-limits" => Of(await store.GetAsOfAsync<TdsThresholdLimits>(day, cancellationToken), set => (set.Version, [.. set.Limits.Select(row => row.Limit)])),
            "bonus.prior-charges" => Of(await store.GetAsOfAsync<BonusPriorCharges>(day, cancellationToken), set => (set.Version, [.. set.Charges.Select(row => row.Item)])),
            "bonus.rules" => Of(await store.GetAsOfAsync<BonusRules>(day, cancellationToken), set => (set.Version, [.. set.Rules.Select(row => row.Provision)])),
            "bonus.state-wage-limit-readings" => Of(await store.GetAsOfAsync<StateWageLimitReadings>(day, cancellationToken), set => (set.Version, [.. set.Readings.Select(row => row.StateCode)])),
            "bonus.wage-limits" => Of(await store.GetAsOfAsync<BonusWageLimits>(day, cancellationToken), set => (set.Version, [.. set.Limits.Select(row => row.Government)])),
            "esi.average-daily-wage-divisors" => Of(await store.GetAsOfAsync<EsiAverageDailyWageDivisors>(day, cancellationToken), set => (set.Version, [.. set.Divisors.Select(row => row.Divisor)])),
            "esi.contribution-due-dates" => Of(await store.GetAsOfAsync<EsiContributionDueDates>(day, cancellationToken), set => (set.Version, [.. set.Rules.Select(row => row.Rule)])),
            "esi.contribution-exemptions" => Of(await store.GetAsOfAsync<EsiContributionExemptions>(day, cancellationToken), set => (set.Version, [.. set.Exemptions.Select(row => row.Exemption)])),
            "esi.contribution-periods" => Of(await store.GetAsOfAsync<EsiContributionPeriods>(day, cancellationToken), set => (set.Version, [.. set.Periods.Select(row => row.Period)])),
            "esi.contribution-rates" => Of(await store.GetAsOfAsync<EsiContributionRates>(day, cancellationToken), set => (set.Version, [.. set.Rates.Select(row => row.Share)])),
            "esi.coverage-wage-limits" => Of(await store.GetAsOfAsync<EsiCoverageWageLimits>(day, cancellationToken), set => (set.Version, [.. set.Limits.Select(row => row.Limit)])),
            "gratuity.rules" => Of(await store.GetAsOfAsync<GratuityRules>(day, cancellationToken), set => (set.Version, [.. set.Rules.Select(row => row.Provision)])),
            "labour-codes.wage-definition" => Of(await store.GetAsOfAsync<WageDefinition>(day, cancellationToken), set => (set.Version, [.. set.Clauses.Select(row => row.Clause)])),
            "labour-welfare-fund.contributions" => Of(await store.GetAsOfAsync<WelfareFundContributions>(day, cancellationToken), set => (set.Version, [.. set.Contributions.Select(row => row.Entry)])),
            "labour-welfare-fund.coverage" => Of(await store.GetAsOfAsync<WelfareFundCoverage>(day, cancellationToken), set => (set.Version, [.. set.Definitions.Select(row => row.Entry)])),
            "labour-welfare-fund.due-days" => Of(await store.GetAsOfAsync<WelfareFundDueDays>(day, cancellationToken), set => (set.Version, [.. set.Days.Select(row => row.Entry)])),
            "professional-tax.exemptions" => Of(await store.GetAsOfAsync<ProfessionalTaxExemptions>(day, cancellationToken), set => (set.Version, [.. set.Exemptions.Select(row => row.Exemption)])),
            "professional-tax.levies" => Of(await store.GetAsOfAsync<ProfessionalTaxLevies>(day, cancellationToken), set => (set.Version, [.. set.States.Select(row => row.StateCode)])),
            "professional-tax.salary-slabs" => Of(await store.GetAsOfAsync<ProfessionalTaxSlabs>(day, cancellationToken), set => (set.Version, [.. set.Slabs.Select(row => row.Slab)])),
            "provident-fund.charges" => Of(await store.GetAsOfAsync<ProvidentFundCharges>(day, cancellationToken), set => (set.Version, [.. set.Charges.Select(row => row.Charge)])),
            "provident-fund.contribution-rates" => Of(await store.GetAsOfAsync<ProvidentFundContributionRates>(day, cancellationToken), set => (set.Version, [.. set.Rates.Select(row => row.Contribution)])),
            "provident-fund.due-dates" => Of(await store.GetAsOfAsync<ProvidentFundDueDates>(day, cancellationToken), set => (set.Version, [.. set.Remittances.Select(row => row.Remittance)])),
            "provident-fund.membership-limits" => Of(await store.GetAsOfAsync<ProvidentFundMembershipLimits>(day, cancellationToken), set => (set.Version, [.. set.Limits.Select(row => row.Limit)])),
            "provident-fund.rounding" => Of(await store.GetAsOfAsync<ProvidentFundRounding>(day, cancellationToken), set => (set.Version, [.. set.Contributions.Select(row => row.Contributions)])),
            "provident-fund.ten-per-cent-establishments" => Of(await store.GetAsOfAsync<TenPerCentEstablishments>(day, cancellationToken), set => (set.Version, [.. set.Entries.Select(row => row.Entry)])),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No typed set reads this key."),
        };
    }

    private static (StatutoryParameterVersion Version, IReadOnlyList<string> Keys)? Of<TSet>(TSet? set, Func<TSet, (StatutoryParameterVersion, IReadOnlyList<string>)> read)
        where TSet : class =>
        set is null ? null : read(set);

    private sealed record UnseededSet(StatutoryParameterVersion Version) : IStatutoryParameterSet<UnseededSet>
    {
        public static string Key => "test.unseeded";

        public static UnseededSet Create(StatutoryParameterVersion version, IStatutoryParameterRows rows) => new(version);
    }
}
