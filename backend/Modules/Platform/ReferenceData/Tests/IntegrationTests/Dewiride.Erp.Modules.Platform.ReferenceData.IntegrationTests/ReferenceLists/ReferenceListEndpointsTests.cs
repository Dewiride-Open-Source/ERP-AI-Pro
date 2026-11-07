using System.Net;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Endpoints.Responses;
using Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;
using Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ReferenceLists;

[Collection(ReferenceRowReaders.Name)]
public sealed class ReferenceListEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string Route = "/api/platform/reference-data";

    [Theory]
    [InlineData(Route + "/countries")]
    [InlineData(Route + "/indian-states")]
    [InlineData(Route + "/currencies")]
    [InlineData(Route + "/financial-years")]
    public void Route_OfEachList_RequiresTheListsPermission(string route)
    {
        Assert.Equal(ReferenceDataPermissions.ReadLists, RoutePermissions.Of(factory.Services, "GET", route));
    }

    [Theory]
    [InlineData("/countries")]
    [InlineData("/indian-states")]
    [InlineData("/currencies")]
    [InlineData("/financial-years?from=2026-27&to=2026-27")]
    public async Task List_AsEmployee_AnswersWithAResponseKeptForAnHour(string suffix)
    {
        using var client = Employee();

        using var response = await client.GetAsync(new Uri(Route + suffix, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromHours(1), response.Headers.CacheControl?.MaxAge);
    }

    [Fact]
    public async Task Countries_AsEmployee_ListsEveryCountryWithItsCode()
    {
        var countries = (await GetAsync<CountriesResponse>("/countries")).Countries;

        Assert.Equal(250, countries.Count);
        Assert.Contains(new CountryResponse("IN", "India"), countries);
        Assert.Contains(new CountryResponse("XK", "Kosovo"), countries);
        Assert.Contains(new CountryResponse("TR", "Türkiye"), countries);
    }

    [Fact]
    public async Task IndianStates_WithoutADate_ListsEveryCodeWithItsDatesByCode()
    {
        var states = (await GetAsync<IndianStatesResponse>("/indian-states")).States;

        Assert.Equal(40, states.Count);
        Assert.Equal(states.OrderBy(state => state.GstCode, StringComparer.Ordinal).ThenBy(state => state.ValidFrom), states);
        Assert.Contains(new IndianStateResponse("25", "Daman and Diu", IndianStateKind.UnionTerritory, new DateOnly(2017, 7, 1), new DateOnly(2020, 7, 31)), states);
    }

    [Theory]
    [InlineData("2026-11-07", 37, false)]
    [InlineData("2020-07-31", 38, true)]
    [InlineData("2020-08-01", 37, false)]
    public async Task IndianStates_OnADate_ListsTheCodesInForceThatDay(string date, int expectedCount, bool damanAndDiu)
    {
        var states = (await GetAsync<IndianStatesResponse>($"/indian-states?on={date}")).States;

        Assert.Equal(expectedCount, states.Count);
        Assert.Equal(damanAndDiu, states.Any(state => state.GstCode == "25"));
        Assert.Equal(states.Count, states.Select(state => state.GstCode).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task IndianStates_KindOnTheWire_IsCamelCase()
    {
        using var client = Employee();

        var body = await client.GetStringAsync(new Uri(Route + "/indian-states?on=2026-11-07", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Contains("\"kind\":\"unionTerritory\"", body, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"otherTerritory\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Currencies_AsEmployee_ListsEveryCurrencyWithItsDecimalPlaces()
    {
        var currencies = (await GetAsync<CurrenciesResponse>("/currencies")).Currencies;

        Assert.Equal(155, currencies.Count);
        Assert.Contains(new CurrencyResponse("INR", "Indian Rupee", 2), currencies);
        Assert.Contains(new CurrencyResponse("JPY", "Yen", 0), currencies);
        Assert.Contains(new CurrencyResponse("KWD", "Kuwaiti Dinar", 3), currencies);
    }

    [Fact]
    public async Task FinancialYears_Range_ListsEachYearWithItsQuartersAndTwelvePeriodsFromApril()
    {
        var years = (await GetAsync<FinancialYearsResponse>("/financial-years?from=2025-26&to=2026-27")).Years;

        Assert.Equal(["2025-26", "2026-27"], years.Select(year => year.Label));
        var year = years[1];
        Assert.Equal((new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)), (year.Start, year.End));
        Assert.Equal(new FinancialQuarterResponse(4, "Q4 2026-27", new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31)), year.Quarters[3]);
        Assert.Equal(12, year.Periods.Count);
        Assert.Equal(new FinancialPeriodResponse(1, "April 2026", 1, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)), year.Periods[0]);
        Assert.Equal(new FinancialPeriodResponse(12, "March 2027", 4, new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31)), year.Periods[11]);
    }

    [Theory]
    [InlineData("?from=2026-28&to=2026-27", "financial-year.label-invalid", "from")]
    [InlineData("?from=2026-27&to=2025-26", "financial-year.range-reversed", "to")]
    [InlineData("?from=2026&to=2026-27", "request.invalid", "from")]
    [InlineData("?to=2026-27", "request.invalid", "from")]
    public async Task FinancialYears_RangeThatIsNoRange_Returns400KeyedByItsParameter(string query, string code, string member)
    {
        using var client = Employee();

        using var response = await client.GetAsync(new Uri(Route + "/financial-years" + query, UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, code);
        Assert.Equal(member, Assert.Single(problem.Errors.Keys));
    }

    private async Task<T> GetAsync<T>(string suffix)
    {
        using var client = Employee();
        using var response = await client.GetAsync(new Uri(Route + suffix, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ExchangeRatesApi.ReadAsync<T>(response);
    }

    private HttpClient Employee() => factory.CreateClient().AsUser(TestUsers.Accountant);
}
