using System.Net;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;

public sealed class ExchangeRateImportTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Import_NewRates_RecordsEveryRowOfTheFile()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();

        using var response = await ExchangeRatesApi.ImportAsync(
            client,
            ExchangeRatesApi.Rate("USD", day, 84.1234m, source: "FBIL"),
            ExchangeRatesApi.Rate("JPY", day, 56.35m, 100),
            ExchangeRatesApi.Rate("IDR", day, 51.902m, 10_000));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ImportedExchangeRatesResponse(3, 0), await ExchangeRatesApi.ReadAsync<ImportedExchangeRatesResponse>(response));
        var page = await RatesOfAsync(client, day);
        Assert.Equal([("IDR", 51.902m, 10_000), ("JPY", 56.35m, 100), ("USD", 84.1234m, 1)], page.Select(rate => (rate.Currency, rate.Rate, rate.Unit)));
    }

    [Fact]
    public async Task Import_SameFileTwice_LeavesTheRecordedRatesAsTheyWere()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();
        object[] file = [ExchangeRatesApi.Rate("EUR", day, 97.5m), ExchangeRatesApi.Rate("GBP", day, 112m)];
        using (var first = await ExchangeRatesApi.ImportAsync(client, file))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var recorded = await RatesOfAsync(client, day);

        using var second = await ExchangeRatesApi.ImportAsync(client, file);

        Assert.Equal(new ImportedExchangeRatesResponse(0, 2), await ExchangeRatesApi.ReadAsync<ImportedExchangeRatesResponse>(second));
        Assert.Equal(recorded, await RatesOfAsync(client, day));
    }

    [Fact]
    public async Task Import_RowThatBreaksARule_RecordsNoRowAndKeysTheProblemByItsRow()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();

        using var response = await ExchangeRatesApi.ImportAsync(
            client,
            ExchangeRatesApi.Rate("USD", day, 84m),
            ExchangeRatesApi.Rate("INR", day, 1m),
            ExchangeRatesApi.Rate("EUR", day, 97.12345m));

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, "exchange-rate.import-refused");
        Assert.Equal(["rates[1].currency", "rates[2].rate"], problem.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(await RatesOfAsync(client, day));
    }

    [Fact]
    public async Task Import_TwoRowsOfOneCurrencyAndDay_RefusesTheLaterRow()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();

        using var response = await ExchangeRatesApi.ImportAsync(client, ExchangeRatesApi.Rate("USD", day, 84m), ExchangeRatesApi.Rate("USD", day, 84m));

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, "exchange-rate.import-refused");
        Assert.Contains("Row 1", Assert.Single(problem.Errors["rates[1].rateDate"]), StringComparison.Ordinal);
        Assert.Empty(await RatesOfAsync(client, day));
    }

    [Fact]
    public async Task Import_RowDifferingFromARecordedRate_RefusesItAndKeepsTheRecordedRate()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();
        var recorded = await ExchangeRatesApi.RecordAsync(client, "USD", day, 84m);

        using var response = await ExchangeRatesApi.ImportAsync(client, ExchangeRatesApi.Rate("USD", day, 84.5m), ExchangeRatesApi.Rate("EUR", day, 97m));

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, "exchange-rate.import-refused");
        Assert.Equal("rates[0].rate", Assert.Single(problem.Errors.Keys));
        Assert.Equal([(recorded.Id, 84m)], (await RatesOfAsync(client, day)).Select(rate => (rate.Id, rate.Rate)));
    }

    [Fact]
    public async Task Import_RowWithoutARate_Returns400KeyedByTheRowsField()
    {
        using var client = Administrator();

        using var response = await ExchangeRatesApi.ImportAsync(client, new { currency = "USD", rateDate = ExchangeRatesApi.Day(ExchangeRatesApi.UniqueDay()), unit = 1 });

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, "request.invalid");
        Assert.Equal("rates[0].rate", Assert.Single(problem.Errors.Keys));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_001)]
    public async Task Import_NoRowOrMoreThanAThousand_Returns400KeyedByRates(int rows)
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();
        var file = Enumerable.Range(0, rows).Select(row => ExchangeRatesApi.Rate("USD", day.AddDays(-row), 84m)).ToArray();

        using var response = await ExchangeRatesApi.ImportAsync(client, file);

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, "request.invalid");
        Assert.Equal("rates", Assert.Single(problem.Errors.Keys));
    }

    private static async Task<List<ExchangeRateResponse>> RatesOfAsync(HttpClient client, DateOnly day)
    {
        var page = await ExchangeRatesApi.ListAsync(client, $"sort=currency:asc&filter={ExchangeRatesApi.Filter($"rateDate:eq:{ExchangeRatesApi.Day(day)}")}");

        return [.. page.Items];
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
