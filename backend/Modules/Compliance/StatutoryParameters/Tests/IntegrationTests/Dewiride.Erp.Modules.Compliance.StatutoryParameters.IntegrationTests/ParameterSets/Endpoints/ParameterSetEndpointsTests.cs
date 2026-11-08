using System.Globalization;
using System.Net;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;

[Collection(StatutoryRows.Name)]
public sealed class ParameterSetEndpointsTests(ErpApiFactory factory)
{
    private const string CentralTaxRates = "gst.central-tax-rates";

    private static readonly string[] SeededKeys =
    [
        "bonus.prior-charges",
        "bonus.rules",
        "bonus.state-wage-limit-readings",
        "bonus.wage-limits",
        "companies.schedule-ii-limits",
        "companies.schedule-ii-useful-lives",
        "esi.average-daily-wage-divisors",
        "esi.contribution-due-dates",
        "esi.contribution-exemptions",
        "esi.contribution-periods",
        "esi.contribution-rates",
        "esi.coverage-wage-limits",
        "gratuity.rules",
        "gst.central-tax-rates",
        "gst.integrated-tax-rates",
        "gst.quarterly-return-states",
        "gst.return-due-dates",
        "gst.return-thresholds",
        "gst.sac-codes",
        "gst.sac-rate-entries",
        "gst.union-territory-tax-rates",
        "income-tax.advance-tax-instalments",
        "income-tax.advance-tax-interest",
        "income-tax.company-rates",
        "income-tax.company-surcharge",
        "income-tax.depreciation-allowances",
        "income-tax.depreciation-notes",
        "income-tax.depreciation-rates",
        "income-tax.health-and-education-cess",
        "income-tax.minimum-alternate-tax",
        "income-tax.tds-crosswalk",
        "income-tax.tds-declarations",
        "income-tax.tds-entries",
        "income-tax.tds-higher-rates",
        "income-tax.tds-no-deduction-cases",
        "income-tax.tds-rates",
        "income-tax.tds-rates-in-force",
        "income-tax.tds-rates-in-force-readings",
        "income-tax.tds-surcharge-and-cess",
        "income-tax.tds-terms",
        "income-tax.tds-threshold-limits",
        "labour-codes.wage-definition",
        "labour-welfare-fund.contributions",
        "labour-welfare-fund.coverage",
        "labour-welfare-fund.due-days",
        "professional-tax.exemptions",
        "professional-tax.levies",
        "professional-tax.salary-slabs",
        "provident-fund.charges",
        "provident-fund.contribution-rates",
        "provident-fund.due-dates",
        "provident-fund.membership-limits",
        "provident-fund.rounding",
        "provident-fund.ten-per-cent-establishments",
    ];

    [Theory]
    [InlineData(ParameterSetsApi.Route)]
    [InlineData(ParameterSetsApi.Route + "/{key}")]
    public void Route_OfEachUseCase_RequiresTheReadPermission(string route)
    {
        Assert.Equal(StatutoryParametersPermissions.ReadParameterSets, RoutePermissions.Of(factory.Services, "GET", route));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/" + CentralTaxRates)]
    [InlineData("/" + CentralTaxRates + "?on=2019-10-01")]
    public async Task Get_AsAdministrator_AnswersWithAResponseKeptAnHourForTheSignInThatReadIt(string suffix)
    {
        using var client = Administrator();

        using var response = await client.GetAsync(ParameterSetsApi.Path(suffix), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromHours(1), response.Headers.CacheControl?.MaxAge);
        Assert.Equal(["Authorization", "Cookie"], response.Headers.Vary);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/" + CentralTaxRates)]
    public async Task Get_AsEmployee_Returns403RequestForbidden(string suffix)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(ParameterSetsApi.Path(suffix), TestContext.Current.CancellationToken);

        await ParameterSetsApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "request.forbidden");
    }

    [Fact]
    public async Task List_AsAdministrator_AnswersEverySeededSetByTitle()
    {
        using var client = Administrator();

        var sets = (await ParameterSetsApi.ListAsync(client)).Sets;

        Assert.Equal(SeededKeys, sets.Select(set => set.Key).Order(StringComparer.Ordinal));
        Assert.Equal(sets.Select(set => set.Title).Order(StringComparer.OrdinalIgnoreCase), sets.Select(set => set.Title));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task List_EachSet_AgreesWithItsOwnPage(string key)
    {
        using var client = Administrator();

        var summary = Assert.Single((await ParameterSetsApi.ListAsync(client)).Sets, set => set.Key == key);
        var set = await ParameterSetsApi.GetAsync(client, key);

        Assert.Equal((set.Title, set.Description, set.Basis, set.AsOf), (summary.Title, summary.Description, summary.Basis, summary.AsOf));
        Assert.Equal(set.Versions.Count, summary.VersionCount);
        Assert.Equal(set.Versions[^1].From, summary.FirstVersionFrom);
        AssertSameVersion(set.Versions.SingleOrDefault(version => version.From == set.CurrentFrom), summary.Current);
        AssertSameVersion(set.Versions.Where(version => set.CurrentFrom is not { } current || version.From > current).MinBy(version => version.From), summary.Next);
        Assert.Equal(set.Shown?.Rows.Count ?? 0, summary.EntryCount);
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task Get_EachSet_AnswersItsVersionsNewestFirstEachRunningToTheDayBeforeTheNext(string key)
    {
        using var client = Administrator();

        var set = await ParameterSetsApi.GetAsync(client, key);

        Assert.Equal(set.Versions.OrderByDescending(version => version.From).Select(version => version.From), set.Versions.Select(version => version.From));
        Assert.Null(set.Versions[0].Until);
        Assert.All(set.Versions.Skip(1).Zip(set.Versions), pair => Assert.Equal(pair.Second.From.AddDays(-1), pair.First.Until));
        Assert.All(set.Versions, version => Assert.NotEmpty(version.Instruments));
        Assert.Equal(set.CurrentFrom, set.Shown?.From);
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task Get_OnEachVersionsDate_ShowsThatVersionWithAsManyChangesAsItsSummaryCounts(string key)
    {
        using var client = Administrator();
        var set = await ParameterSetsApi.GetAsync(client, key);

        foreach (var version in set.Versions)
        {
            var shown = (await ParameterSetsApi.GetAsync(client, key, Day(version.From))).Shown!;

            Assert.Equal((version.From, version.Until), (shown.From, shown.Until));
            Assert.Equal(version.Instruments, shown.Instruments);
            Assert.Equal((version.AddedCount, version.ChangedCount, version.RemovedCount), (shown.Added.Count, shown.Changed.Count, shown.Removed.Count));
            Assert.All(shown.Rows, row => Assert.Equal(set.Columns.Select(column => column.Name), row.Cells.Select(cell => cell.Column)));
        }
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task Get_OnTheDayBeforeTheFirstVersion_ShowsNoVersion(string key)
    {
        using var client = Administrator();
        var firstVersion = (await ParameterSetsApi.GetAsync(client, key)).Versions[^1].From;

        var set = await ParameterSetsApi.GetAsync(client, key, Day(firstVersion.AddDays(-1)));

        Assert.Null(set.Shown);
        Assert.NotNull(set.CurrentFrom);
    }

    [Fact]
    public async Task Get_SetOfStateCodes_DescribesItsColumnsAndWritesEnumsInCamelCase()
    {
        using var client = Administrator();

        var set = await ParameterSetsApi.GetAsync(client, "gst.quarterly-return-states");
        var body = await client.GetStringAsync(ParameterSetsApi.Path("/gst.quarterly-return-states"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [("stateCode", ParameterColumnKind.StateCode, true, false), ("listedAs", ParameterColumnKind.Text, false, true), ("rule", ParameterColumnKind.Code, false, false)],
            set.Columns.Select(column => (column.Name, column.Kind, column.IsKey, column.IsRowTitle)));
        Assert.Equal((ParameterSetBasis.Law, ParameterAsOf.TaxPeriodStart), (set.Basis, set.AsOf));
        Assert.Contains("\"kind\":\"stateCode\"", body, StringComparison.Ordinal);
        Assert.Contains("\"asOf\":\"taxPeriodStart\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("references", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_SetOfStateCodes_NamesTheStateOfEachStateCodeCellAndNoOtherCell()
    {
        using var client = Administrator();

        var set = await ParameterSetsApi.GetAsync(client, "gst.quarterly-return-states");

        var rows = Assert.IsType<ParameterVersionResponse>(set.Shown).Rows;
        Assert.Equal(
            [("stateCode", "Maharashtra"), ("listedAs", null), ("rule", null)],
            Assert.Single(rows, row => row.Key == "27").Cells.Select(cell => (cell.Column, cell.StateName)));
        Assert.All(rows, row => Assert.False(string.IsNullOrEmpty(row.Cells.Single(cell => cell.Column == "stateCode").StateName)));
    }

    [Fact]
    public async Task Get_SetWithoutStateCodes_NamesNoState()
    {
        using var client = Administrator();

        var set = await ParameterSetsApi.GetAsync(client, CentralTaxRates);

        Assert.All(Assert.IsType<ParameterVersionResponse>(set.Shown).Rows.SelectMany(row => row.Cells), cell => Assert.Null(cell.StateName));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task Get_EachSet_DescribesItselfAndItsColumnsWithTheirChoicesAsItsFileDeclaresThem(string key)
    {
        using var client = Administrator();
        var file = SeedFileSets.Of(key).Set;

        var set = await ParameterSetsApi.GetAsync(client, key);

        Assert.Equal((file.Title, file.Description, file.Basis.ToString(), file.AsOf.ToString()), (set.Title, set.Description, set.Basis.ToString(), set.AsOf.ToString()));
        Assert.Equal(
            file.Columns.Columns.Select(column => (column.Name, column.Title, column.Kind.ToString(), column.IsKey, column.IsRowTitle, column.IsOptional)),
            set.Columns.Select(column => (column.Name, column.Title, column.Kind.ToString(), column.IsKey, column.IsRowTitle, column.IsOptional)));
        Assert.Equal(
            file.Columns.Columns.Select(column => string.Join("; ", column.Choices.Select(choice => $"{choice.Value}={choice.Title}"))),
            set.Columns.Select(column => string.Join("; ", column.Choices.Select(choice => $"{choice.Value}={choice.Title}"))));
    }

    [Fact]
    public async Task Get_SacCodes_TitlesEachLevelOfItsChoiceColumn()
    {
        using var client = Administrator();

        var codes = await ParameterSetsApi.GetAsync(client, "gst.sac-codes");

        Assert.Equal(
            [("heading", "Heading"), ("group", "Group"), ("service", "Service")],
            Assert.Single(codes.Columns, column => column.Kind == ParameterColumnKind.Choice).Choices.Select(choice => (choice.Value, choice.Title)));
    }

    [Fact]
    public async Task Get_UnknownKey_Returns404ParameterSetNotFound()
    {
        using var client = Administrator();

        using var response = await client.GetAsync(ParameterSetsApi.Path("/gst.no-such-set"), TestContext.Current.CancellationToken);

        await ParameterSetsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, "parameter-set.not-found");
    }

    [Theory]
    [InlineData("GST.central-tax-rates")]
    [InlineData("gst")]
    [InlineData("gst..rates")]
    [InlineData("gst.central_tax_rates")]
    [InlineData("gst.central-tax-rates-")]
    [InlineData("gst.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghi")]
    public async Task Get_KeyOffThePattern_Returns400KeyedByKey(string key)
    {
        using var client = Administrator();

        using var response = await client.GetAsync(ParameterSetsApi.Path($"/{Uri.EscapeDataString(key)}"), TestContext.Current.CancellationToken);

        var problem = await ParameterSetsApi.AssertInvalidRequestAsync(response);
        Assert.Equal("key", Assert.Single(problem.Errors.Keys));
    }

    [Theory]
    [InlineData("2026-13-01")]
    [InlineData("yesterday")]
    public async Task Get_MalformedDate_Returns400RequestMalformed(string on)
    {
        using var client = Administrator();

        using var response = await client.GetAsync(ParameterSetsApi.Path($"/{CentralTaxRates}?on={on}"), TestContext.Current.CancellationToken);

        await ParameterSetsApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "request.malformed");
    }

    public static TheoryData<string> Keys() => [.. SeededKeys];

    private static void AssertSameVersion(ParameterVersionSummaryResponse? expected, ParameterVersionSummaryResponse? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.Equal(
            (expected.From, expected.Until, expected.AddedCount, expected.ChangedCount, expected.RemovedCount),
            (actual.From, actual.Until, actual.AddedCount, actual.ChangedCount, actual.RemovedCount));
        Assert.Equal(expected.Instruments, actual.Instruments);
    }

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
