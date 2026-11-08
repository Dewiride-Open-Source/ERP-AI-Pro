using System.Net;
using System.Text.Json;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.EndToEnd;
using Dewiride.Erp.Testing.EndToEnd.Numbering;
using Dewiride.Erp.Testing.Numbering;

namespace Dewiride.Erp.Host.Api.IntegrationTests.EndToEnd;

// Each test that sets a numbering up takes a test kind of its own in the test process's database; the formats use letters and
// digits alone, because the other characters a number may contain are statutory.
public sealed class NumberingRoutesTests(NumberingRoutesTests.Fixture fixture) : IClassFixture<NumberingRoutesTests.Fixture>
{
    private const string NumberingRoute = "/api/platform/numbering";

    [Fact]
    public async Task Post_IssuesOfATestKindSetUp_AnswersItsNextNumbersInTurn()
    {
        var document = TestNumberedDocuments.Numbered(14);
        await SetUpAsync(document.Key);
        using var anonymous = fixture.Factory.CreateClient();

        var first = await IssueAsync(anonymous, document.Key, 2);
        var second = await IssueAsync(anonymous, document.Key, 1);

        Assert.Equal(["TR001", "TR002"], first);
        Assert.Equal(["TR003"], second);
    }

    [Fact]
    public async Task Post_ResetsOfATestKind_RemovesItsNumberingWithItsYears()
    {
        var document = TestNumberedDocuments.Numbered(15);
        await SetUpAsync(document.Key);
        using var anonymous = fixture.Factory.CreateClient();
        await IssueAsync(anonymous, document.Key, 1);

        using var reset = await anonymous.PostAsJsonAsync(new Uri(NumberingRoutes.ResetsPath, UriKind.Relative), new { documentType = document.Key }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await DocumentAsync(document.Key)).GetProperty("series").ValueKind);
        using var again = await anonymous.PostAsJsonAsync(new Uri(NumberingRoutes.ResetsPath, UriKind.Relative), new { documentType = document.Key }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Theory]
    [InlineData("finance.sales.tax-invoices", 1)]
    [InlineData("test.numbering.invoices-01", 0)]
    [InlineData("test.numbering.invoices-01", NumberingRoutes.MaxCount + 1)]
    public async Task Post_IssuesOfAnotherKindOrOutsideTheCount_Answers400(string documentType, int count)
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(new Uri(NumberingRoutes.IssuesPath, UriKind.Relative), new { documentType, count }, TestContext.Current.CancellationToken);

        Assert.Equal(["documentType"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Post_ResetsOfAKindOtherThanTheTestKinds_Answers400()
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(new Uri(NumberingRoutes.ResetsPath, UriKind.Relative), new { documentType = "finance.sales.tax-invoices" }, TestContext.Current.CancellationToken);

        Assert.Equal(["documentType"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Map_InTheDevelopmentEnvironment_StopsTheHostFromStarting()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(NumberingRoutes.Map);

        var exception = Assert.IsType<InvalidOperationException>(factory.StartFailure());

        Assert.Contains(EndToEndHost.EnvironmentName, exception.Message, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<string>> IssueAsync(HttpClient anonymous, string documentType, int count)
    {
        using var response = await anonymous.PostAsJsonAsync(new Uri(NumberingRoutes.IssuesPath, UriKind.Relative), new { documentType, count }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<NumberIssueResponse>(TestContext.Current.CancellationToken))!.Numbers;
    }

    private static async Task<IReadOnlyList<string>> ErrorKeysAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);

        return [.. problem!.Errors.Keys.Order(StringComparer.Ordinal)];
    }

    private async Task SetUpAsync(string documentType)
    {
        using var administrator = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{NumberingRoute}/series", UriKind.Relative))
        {
            Content = JsonContent.Create(new { documentType, format = new { prefix = "TR", year = "none", separator = string.Empty, digits = 3 }, nextNumber = 1 }),
        };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());

        using var response = await administrator.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<JsonElement> DocumentAsync(string documentType)
    {
        using var administrator = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        using var response = await administrator.GetAsync(new Uri($"{NumberingRoute}/documents", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var documents = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return Assert.Single(documents.RootElement.EnumerateArray(), document => document.GetProperty("documentType").GetString() == documentType).Clone();
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = ErpApiFactory.ForEnvironment(EndToEndHost.EnvironmentName)
            .WithTestServices(TestNumberedDocuments.Register)
            .WithTestEndpoints(NumberingRoutes.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
