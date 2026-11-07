using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.Countries;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.ExchangeRates;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;
using Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.Seeding;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.PublicApi;

// The Mongolian tugrik is recorded by this test alone, so the rates it reads back are its own.
[Collection(ReferenceRowReaders.Name)]
public sealed class ReferenceDataLookupTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task RateOnAsync_DatesAroundTwoRates_AnswersTheLatestOnOrBeforeEachDate()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var first = ExchangeRatesApi.UniqueDay();
        var gap = ExchangeRatesApi.UniqueDay();
        var second = ExchangeRatesApi.UniqueDay();
        var tugrik = Currency.FromCode("MNT");
        await ExchangeRatesApi.RecordAsync(client, "MNT", first, 2.41m, 100);
        var later = await ExchangeRatesApi.RecordAsync(client, "MNT", second, 2.43m, 100);

        Assert.Null(await RateOnAsync(tugrik, first.AddDays(-1)));
        Assert.Equal(new ExchangeRateEntry(tugrik, first, 2.41m, 100), await RateOnAsync(tugrik, first));
        Assert.Equal(new ExchangeRateEntry(tugrik, first, 2.41m, 100), await RateOnAsync(tugrik, gap));
        Assert.Equal(new ExchangeRateEntry(tugrik, second, 2.43m, 100), await RateOnAsync(tugrik, second.AddDays(30)));

        using (var removed = await client.DeleteAsync(ExchangeRatesApi.Path($"/{later.Id}"), TestContext.Current.CancellationToken))
        {
            removed.EnsureSuccessStatusCode();
        }

        Assert.Equal(new ExchangeRateEntry(tugrik, first, 2.41m, 100), await RateOnAsync(tugrik, second));
    }

    [Theory]
    [InlineData("25", "2020-07-31", "Daman and Diu")]
    [InlineData("25", "2020-08-01", null)]
    [InlineData("26", "2020-01-25", "Dadra and Nagar Haveli")]
    [InlineData("26", "2020-01-26", "Dadra and Nagar Haveli and Daman and Diu")]
    [InlineData("38", "2019-12-31", null)]
    [InlineData("38", "2020-01-01", "Ladakh")]
    [InlineData("28", "2026-11-07", null)]
    public async Task FindAsync_GstCodeOnADate_NamesTheTerritoryItStoodFor(string code, string date, string? name)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var state = await scope.ServiceProvider.GetRequiredService<IIndianStates>()
            .FindAsync(code, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);

        Assert.Equal(name, state?.Name);
    }

    [Theory]
    [InlineData("IN", "India")]
    [InlineData("XK", "Kosovo")]
    [InlineData("ZZ", null)]
    [InlineData("in", null)]
    public async Task FindAsync_CountryCode_NamesTheCountry(string code, string? name)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var country = await scope.ServiceProvider.GetRequiredService<ICountries>().FindAsync(code, TestContext.Current.CancellationToken);

        Assert.Equal(name, country?.Name);
    }

    private async Task<ExchangeRateEntry?> RateOnAsync(Currency currency, DateOnly date)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IExchangeRates>().RateOnAsync(currency, date, TestContext.Current.CancellationToken);
    }
}
