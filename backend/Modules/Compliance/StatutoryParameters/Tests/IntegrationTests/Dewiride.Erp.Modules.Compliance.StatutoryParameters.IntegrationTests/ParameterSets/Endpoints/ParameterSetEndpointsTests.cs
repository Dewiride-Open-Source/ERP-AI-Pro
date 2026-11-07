using System.Globalization;
using System.Net;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.Seeding;
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
        "gst.central-tax-rates",
        "gst.integrated-tax-rates",
        "gst.quarterly-return-states",
        "gst.return-due-dates",
        "gst.return-thresholds",
        "gst.sac-codes",
        "gst.sac-rate-entries",
        "gst.union-territory-tax-rates",
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
    public async Task Get_AsAdministrator_AnswersWithAResponseKeptForAnHour(string suffix)
    {
        using var client = Administrator();

        using var response = await client.GetAsync(ParameterSetsApi.Path(suffix), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromHours(1), response.Headers.CacheControl?.MaxAge);
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
        var current = set.Versions.Single(version => version.From == set.CurrentFrom);
        Assert.NotNull(summary.Current);
        Assert.Equal(
            (current.From, current.Until, current.AddedCount, current.ChangedCount, current.RemovedCount),
            (summary.Current.From, summary.Current.Until, summary.Current.AddedCount, summary.Current.ChangedCount, summary.Current.RemovedCount));
        Assert.Equal(current.Instruments, summary.Current.Instruments);
        Assert.Null(summary.Next);
        Assert.Equal(set.Shown!.Rows.Count, summary.EntryCount);
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
    public async Task Get_ChoiceColumn_ListsItsChoicesWithTheirTitles()
    {
        using var client = Administrator();

        var set = await ParameterSetsApi.GetAsync(client, "gst.sac-rate-entries");
        var codes = await ParameterSetsApi.GetAsync(client, "gst.sac-codes");

        Assert.Equal(ParameterSetBasis.Interpretation, set.Basis);
        Assert.All(set.Columns, column => Assert.Empty(column.Choices));
        Assert.Equal(["heading", "group", "service"], Assert.Single(codes.Columns, column => column.Kind == ParameterColumnKind.Choice).Choices.Select(choice => choice.Value));
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

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
