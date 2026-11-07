using System.Net;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints.Responses;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authorization;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ExchangeRates.Endpoints;

public sealed class ExchangeRateEndpointsTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData("GET", ExchangeRatesApi.Route, ReferenceDataPermissions.ReadExchangeRates)]
    [InlineData("GET", ExchangeRatesApi.Route + "/{id:guid}", ReferenceDataPermissions.ReadExchangeRates)]
    [InlineData("POST", ExchangeRatesApi.Route, ReferenceDataPermissions.RecordExchangeRates)]
    [InlineData("POST", ExchangeRatesApi.Route + "/imports", ReferenceDataPermissions.RecordExchangeRates)]
    [InlineData("PUT", ExchangeRatesApi.Route + "/{id:guid}", ReferenceDataPermissions.CorrectExchangeRates)]
    [InlineData("DELETE", ExchangeRatesApi.Route + "/{id:guid}", ReferenceDataPermissions.CorrectExchangeRates)]
    public void Route_OfEachUseCase_RequiresItsOwnPermission(string method, string route, string permission)
    {
        Assert.Equal(permission, RoutePermissions.Of(factory.Services, method, route));
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/{id}")]
    [InlineData("POST", "")]
    [InlineData("POST", "/imports")]
    [InlineData("PUT", "/{id}")]
    [InlineData("DELETE", "/{id}")]
    public async Task EveryRoute_AsEmployee_Returns403RequestForbidden(string method, string suffix)
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);
        using var request = new HttpRequestMessage(new HttpMethod(method), ExchangeRatesApi.Path(suffix.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await ExchangeRatesApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "request.forbidden");
    }

    [Fact]
    public async Task Record_AsAdministrator_Returns201WithTheRateAsPublishedAtItsLocation()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();

        using var response = await ExchangeRatesApi.PostAsync(client, ExchangeRatesApi.Rate("JPY", day, 56.35m, 100, "  FBIL reference rate "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var recorded = await ExchangeRatesApi.ReadAsync<ExchangeRateResponse>(response);
        Assert.Equal(ExchangeRatesApi.Path($"/{recorded.Id}"), response.Headers.Location);
        Assert.Equal(("JPY", day, 56.35m, 100, "FBIL reference rate"), (recorded.Currency, recorded.RateDate, recorded.Rate, recorded.Unit, recorded.Source));
        Assert.Equal(7, recorded.Id.Version);
        Assert.False(string.IsNullOrEmpty(recorded.Version));
    }

    [Fact]
    public async Task Record_SameIdempotencyKeyTwice_RecordsTheRateOnceAndReplaysTheAnswer()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();
        var key = Guid.CreateVersion7().ToString();

        using var first = await ExchangeRatesApi.PostAsync(client, ExchangeRatesApi.Rate("USD", day, 84.1234m), idempotencyKey: key);
        using var second = await ExchangeRatesApi.PostAsync(client, ExchangeRatesApi.Rate("USD", day, 84.1234m), idempotencyKey: key);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("true", Assert.Single(second.Headers.GetValues(ExchangeRatesApi.IdempotencyReplayedHeader)));
        var recorded = await ExchangeRatesApi.ReadAsync<ExchangeRateResponse>(first);
        var page = await ExchangeRatesApi.ListAsync(client, $"filter={ExchangeRatesApi.Filter($"rateDate:eq:{ExchangeRatesApi.Day(day)}")}");
        Assert.Equal(recorded.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Record_SameCurrencyAndDayAgain_Returns409AlreadyRecorded()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();
        await ExchangeRatesApi.RecordAsync(client, "EUR", day, 97.5m);

        using var response = await ExchangeRatesApi.PostAsync(client, ExchangeRatesApi.Rate("EUR", day, 98m));

        await ExchangeRatesApi.AssertProblemAsync(response, HttpStatusCode.Conflict, "exchange-rate.already-recorded");
    }

    [Theory]
    [InlineData("INR", 0, "1", "exchange-rate.currency-not-foreign", "currency")]
    [InlineData("ABC", 0, "1", "exchange-rate.currency-unknown", "currency")]
    [InlineData("XAU", 0, "1", "exchange-rate.currency-unknown", "currency")]
    [InlineData("USD", 400, "1", "exchange-rate.date-in-future", "rateDate")]
    [InlineData("USD", 0, "84.12345", "exchange-rate.rate-too-precise", "rate")]
    [InlineData("USD", 0, "0", "exchange-rate.rate-out-of-range", "rate")]
    public async Task Record_RateThatBreaksARule_Returns400KeyedByItsField(string currency, int daysAhead, string rate, string code, string member)
    {
        using var client = Administrator();
        var day = daysAhead == 0 ? ExchangeRatesApi.UniqueDay() : DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime).AddDays(daysAhead);

        using var response = await ExchangeRatesApi.PostAsync(client, ExchangeRatesApi.Rate(currency, day, decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture)));

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, code);
        Assert.Equal(member, Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task Record_UnitThatIsNoPowerOfTen_Returns400KeyedByUnit()
    {
        using var client = Administrator();

        using var response = await ExchangeRatesApi.PostAsync(client, ExchangeRatesApi.Rate("USD", ExchangeRatesApi.UniqueDay(), 84m, 50));

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, "exchange-rate.unit-invalid");
        Assert.Equal("unit", Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task Record_EmptyBody_Returns400NamingEveryRequiredField()
    {
        using var client = Administrator();

        using var response = await ExchangeRatesApi.PostAsync(client, new { });

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, "request.invalid");
        Assert.Equal(["currency", "rate", "rateDate", "unit"], problem.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Record_AsAdministrator_RecordsTheRateUnderTheModulesChangeKeyNamedByCurrencyAndDay()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();

        var recorded = await ExchangeRatesApi.RecordAsync(client, "GBP", day, 112.25m);

        var change = Assert.Single(await ExchangeRatesApi.ChangesOfAsync(client, recorded.Id));
        Assert.Equal(ChangeOperation.Created, change.Operation);
        Assert.Equal(ExchangeRatesApi.ChangeKey, change.EntityType);
        Assert.Equal($"GBP on {day:dd-MM-yyyy}", change.SubjectName);
        Assert.Equal(TestUsers.Administrator.ObjectId, change.ActorId);
        Assert.Contains(change.Changes, value => value is { Property: "rate", Before: null, After: "112.25" or "112.2500" });
        Assert.Contains(change.Changes, value => value is { Property: "currency", After: "GBP" });
    }

    [Fact]
    public async Task Get_UnknownId_Returns404NotFound()
    {
        using var client = Administrator();

        using var response = await client.GetAsync(ExchangeRatesApi.Path($"/{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        await ExchangeRatesApi.AssertProblemAsync(response, HttpStatusCode.NotFound, "exchange-rate.not-found");
    }

    [Fact]
    public async Task List_WithoutASort_ReturnsTheNewestDayFirstAndCurrenciesInCodeOrder()
    {
        using var client = Administrator();
        var older = ExchangeRatesApi.UniqueDay();
        var newer = ExchangeRatesApi.UniqueDay();
        await ExchangeRatesApi.RecordAsync(client, "USD", older, 83m);
        await ExchangeRatesApi.RecordAsync(client, "USD", newer, 84m);
        await ExchangeRatesApi.RecordAsync(client, "EUR", newer, 97m);
        var filter = ExchangeRatesApi.Filter($"rateDate:in:{ExchangeRatesApi.Day(older)}|{ExchangeRatesApi.Day(newer)}");

        var page = await ExchangeRatesApi.ListAsync(client, $"filter={filter}");

        Assert.Equal([("EUR", newer), ("USD", newer), ("USD", older)], page.Items.Select(rate => (rate.Currency, rate.RateDate)));
    }

    [Fact]
    public async Task List_FilteredByCurrency_ReturnsOnlyThatCurrency()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();
        await ExchangeRatesApi.RecordAsync(client, "USD", day, 84m);
        var aed = await ExchangeRatesApi.RecordAsync(client, "AED", day, 22.9m);
        var filter = ExchangeRatesApi.Filter($"currency:eq:AED;rateDate:eq:{ExchangeRatesApi.Day(day)}");

        var page = await ExchangeRatesApi.ListAsync(client, $"filter={filter}");

        Assert.Equal(aed.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Correct_WithItsVersion_ReplacesTheRateAndRecordsTheChange()
    {
        using var client = Administrator();
        var recorded = await ExchangeRatesApi.RecordAsync(client, "SGD", ExchangeRatesApi.UniqueDay(), 64.1m, source: "Bank");

        using var response = await ExchangeRatesApi.CorrectAsync(client, recorded.Id, new { rate = 64.15m, unit = 1, source = (string?)null, version = recorded.Version });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var corrected = await ExchangeRatesApi.ReadAsync<ExchangeRateResponse>(response);
        Assert.Equal((64.15m, 1, (string?)null), (corrected.Rate, corrected.Unit, corrected.Source));
        Assert.NotEqual(recorded.Version, corrected.Version);
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Updated], (await ExchangeRatesApi.ChangesOfAsync(client, recorded.Id)).Select(change => change.Operation));
    }

    [Fact]
    public async Task Correct_FromAVersionThatChangedSince_Returns409AndKeepsTheLaterCorrection()
    {
        using var client = Administrator();
        var recorded = await ExchangeRatesApi.RecordAsync(client, "AUD", ExchangeRatesApi.UniqueDay(), 55m);
        using (var first = await ExchangeRatesApi.CorrectAsync(client, recorded.Id, new { rate = 55.5m, unit = 1, version = recorded.Version }))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var stale = await ExchangeRatesApi.CorrectAsync(client, recorded.Id, new { rate = 56m, unit = 1, version = recorded.Version });

        await ExchangeRatesApi.AssertProblemAsync(stale, HttpStatusCode.Conflict, "concurrency.conflict");
        using var current = await client.GetAsync(ExchangeRatesApi.Path($"/{recorded.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(55.5m, (await ExchangeRatesApi.ReadAsync<ExchangeRateResponse>(current)).Rate);
    }

    [Fact]
    public async Task Correct_VersionTheApiNeverIssued_Returns400KeyedByVersion()
    {
        using var client = Administrator();
        var recorded = await ExchangeRatesApi.RecordAsync(client, "CAD", ExchangeRatesApi.UniqueDay(), 60m);

        using var response = await ExchangeRatesApi.CorrectAsync(client, recorded.Id, new { rate = 61m, unit = 1, version = "not a version" });

        var problem = await ExchangeRatesApi.AssertInvalidAsync(response, "exchange-rate.version-invalid");
        Assert.Equal("version", Assert.Single(problem.Errors.Keys));
    }

    [Fact]
    public async Task Remove_RecordedRate_HidesItAndLetsItsCurrencyAndDayBeRecordedAgain()
    {
        using var client = Administrator();
        var day = ExchangeRatesApi.UniqueDay();
        var recorded = await ExchangeRatesApi.RecordAsync(client, "CHF", day, 104m);

        using var removed = await client.DeleteAsync(ExchangeRatesApi.Path($"/{recorded.Id}"), TestContext.Current.CancellationToken);
        using var found = await client.GetAsync(ExchangeRatesApi.Path($"/{recorded.Id}"), TestContext.Current.CancellationToken);
        var again = await ExchangeRatesApi.RecordAsync(client, "CHF", day, 104.5m);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await ExchangeRatesApi.AssertProblemAsync(found, HttpStatusCode.NotFound, "exchange-rate.not-found");
        Assert.NotEqual(recorded.Id, again.Id);
        var history = await ExchangeRatesApi.ChangesOfAsync(client, recorded.Id);
        Assert.Equal([ChangeOperation.Created, ChangeOperation.Deleted], history.Select(change => change.Operation));
        Assert.All(history, change => Assert.Equal($"CHF on {day:dd-MM-yyyy}", change.SubjectName));
    }

    [Fact]
    public async Task Remove_UnknownId_Returns404NotFound()
    {
        using var client = Administrator();

        using var response = await client.DeleteAsync(ExchangeRatesApi.Path($"/{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        await ExchangeRatesApi.AssertProblemAsync(response, HttpStatusCode.NotFound, "exchange-rate.not-found");
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);
}
