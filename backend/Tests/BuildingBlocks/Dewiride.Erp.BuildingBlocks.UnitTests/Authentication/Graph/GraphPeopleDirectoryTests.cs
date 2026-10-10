using System.Net;
using System.Security.Claims;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Polly.Timeout;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

public sealed class GraphPeopleDirectoryTests
{
    private const string AuthorizationHeader = "Bearer test-graph-token";

    private const string SearchText = "Meera";

    private static readonly Guid ObjectId = new("2b9c5e1f-7a4d-4c3b-9e8f-1a2b3c4d5e6f");

    private static readonly string Meera = $$"""{"id":"{{ObjectId:D}}","displayName":"Meera Nair","mail":"meera@dewiride.com","userPrincipalName":"meera.nair@dewiride.com"}""";

    private readonly FakeLogger<GraphPeopleDirectory> _logger = new();

    private readonly ClaimsPrincipal _person = new(new ClaimsIdentity([new Claim("oid", Guid.CreateVersion7().ToString("D"))], "Cookies"));

    [Fact]
    public async Task SearchAsync_PeopleFound_AsksGraphForAnAdvancedSearchOnBehalfOfTheSignedInPersonAndAnswersThem()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, $$"""{"value":[{{Meera}}]}"""));
        var headers = new StubAuthorizationHeaders();

        var result = await Directory(graph, headers).SearchAsync($" {SearchText} ", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.HasMore);
        Assert.Equal(new DirectoryPerson(ObjectId, "Meera Nair", "meera.nair@dewiride.com", "meera@dewiride.com"), Assert.Single(result.Value.People));
        var request = Assert.Single(graph.Requests);
        Assert.Equal(new Uri(MicrosoftGraph.BaseAddress, "users").AbsolutePath, request.Address.AbsolutePath);
        var query = QueryHelpers.ParseQuery(request.Address.Query);
        Assert.Equal("\"displayName:Meera\" OR \"mail:Meera\" OR \"userPrincipalName:Meera\"", query["$search"].ToString());
        Assert.Equal("id,displayName,mail,userPrincipalName", query["$select"].ToString());
        Assert.Equal("displayName", query["$orderby"].ToString());
        Assert.Equal(DirectorySearch.MaxPeople.ToString(System.Globalization.CultureInfo.InvariantCulture), query["$top"].ToString());
        Assert.Equal(MicrosoftGraph.EventualConsistency, request.ConsistencyLevel);
        Assert.Equal(AuthorizationHeader, request.Authorization);
        Assert.Equal([MicrosoftGraph.ReadBasicProfilesScope], headers.Scopes);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, headers.Options?.AcquireTokenOptions.AuthenticationOptionsName);
        Assert.Same(_person, headers.Principal);
    }

    [Fact]
    public void SearchPath_TextWithQuotesAndBackslashes_EscapesThemInEveryClause()
    {
        var path = GraphPeopleDirectory.SearchPath(" a\"b\\c ");

        var search = QueryHelpers.ParseQuery(new Uri(MicrosoftGraph.BaseAddress, path).Query)["$search"].ToString();
        Assert.Equal("\"displayName:a\\\"b\\\\c\" OR \"mail:a\\\"b\\\\c\" OR \"userPrincipalName:a\\\"b\\\\c\"", search);
    }

    [Fact]
    public async Task SearchAsync_GraphHoldsAnotherPage_SaysMoreMatched()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, $$"""{"value":[{{Meera}}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/users?$skiptoken=next"}"""));

        var result = await Directory(graph).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.True(result.Value.HasMore);
    }

    [Fact]
    public async Task SearchAsync_UsersWithoutAnObjectIdOrASignInName_LeavesThemOutAndNamesOthersByTheirSignInName()
    {
        var graph = new StubGraph(_ => Json(
            HttpStatusCode.OK,
            $$"""{"value":[{"id":"not-a-guid","displayName":"Broken","userPrincipalName":"broken@dewiride.com"},{"id":"{{Guid.CreateVersion7():D}}","displayName":"No Sign-in Name"},{"id":"{{ObjectId:D}}","displayName":" ","mail":" ","userPrincipalName":"meera.nair@dewiride.com"}]}"""));

        var result = await Directory(graph).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(new DirectoryPerson(ObjectId, "meera.nair@dewiride.com", "meera.nair@dewiride.com", null), Assert.Single(result.Value.People));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "directory.access-denied")]
    [InlineData(HttpStatusCode.Forbidden, "directory.access-denied")]
    [InlineData(HttpStatusCode.RequestTimeout, "directory.unavailable")]
    [InlineData(HttpStatusCode.TooManyRequests, "directory.unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "directory.unavailable")]
    [InlineData(HttpStatusCode.BadGateway, "directory.unavailable")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "directory.unavailable")]
    [InlineData(HttpStatusCode.GatewayTimeout, "directory.unavailable")]
    public async Task SearchAsync_GraphRefusesOrFails_AnswersTheErrorAndLogsTheGraphCodeWithoutTheSearchText(HttpStatusCode status, string code)
    {
        var graph = new StubGraph(_ => Json(status, $$$"""{"error":{"code":"Sample_Code","message":"No user matched {{{SearchText}}}."}}"""));

        var result = await Directory(graph).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(code, result.Error?.Code);
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("Sample_Code", record.Message, StringComparison.Ordinal);
        Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SearchText, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_GraphRefusesWithoutAJsonBody_AnswersAccessDenied()
    {
        var graph = new StubGraph(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("Forbidden", Encoding.UTF8, "text/plain") });

        var result = await Directory(graph).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.AccessDenied, result.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SearchAsync_AnswerTheErpCannotHaveCaused_Throws(HttpStatusCode status)
    {
        var graph = new StubGraph(_ => Json(status, """{"error":{"code":"Request_UnsupportedQuery"}}"""));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Directory(graph).SearchAsync(SearchText, TestContext.Current.CancellationToken));

        Assert.Contains("Request_UnsupportedQuery", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SearchText, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindAsync_PersonOfTheDirectory_LooksThemUpByObjectIdAndAnswersThem()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, Meera));

        var result = await Directory(graph).FindAsync(ObjectId, TestContext.Current.CancellationToken);

        Assert.Equal(new DirectoryPerson(ObjectId, "Meera Nair", "meera.nair@dewiride.com", "meera@dewiride.com"), result.Value);
        var request = Assert.Single(graph.Requests);
        Assert.Equal(new Uri(MicrosoftGraph.BaseAddress, $"users/{ObjectId:D}").AbsolutePath, request.Address.AbsolutePath);
        Assert.Equal("id,displayName,mail,userPrincipalName", QueryHelpers.ParseQuery(request.Address.Query)["$select"].ToString());
        Assert.Null(request.ConsistencyLevel);
        Assert.Equal(AuthorizationHeader, request.Authorization);
    }

    [Fact]
    public async Task FindAsync_ObjectIdTheDirectoryDoesNotHold_AnswersPersonNotFound()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.NotFound, """{"error":{"code":"Request_ResourceNotFound"}}"""));

        var result = await Directory(graph).FindAsync(ObjectId, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.PersonNotFound, result.Error);
    }

    [Fact]
    public async Task FindAsync_GraphAnswersAnotherPerson_Throws()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, Meera));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Directory(graph).FindAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_EntraAsksThePersonToSignInAgain_AnswersAccessDeniedWithoutAskingGraph()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, """{"value":[]}"""));
        var refusal = new MsalUiRequiredException("invalid_grant", "AADSTS65001: consent required.", null, UiRequiredExceptionClassification.ConsentRequired);
        var headers = new StubAuthorizationHeaders(new MicrosoftIdentityWebChallengeUserException(refusal, [MicrosoftGraph.ReadBasicProfilesScope], null));

        var result = await Directory(graph, headers).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.AccessDenied, result.Error);
        Assert.Empty(graph.Requests);
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Contains("invalid_grant", record.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(UiRequiredExceptionClassification.ConsentRequired), record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("AADSTS65001", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_MsalAsksForAnInteractionItself_AnswersAccessDenied()
    {
        var headers = new StubAuthorizationHeaders(new MsalUiRequiredException("user_null", "No account in the cache."));

        var result = await Directory(new StubGraph(_ => throw new InvalidOperationException("Graph is never asked.")), headers).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.AccessDenied, result.Error);
    }

    [Fact]
    public async Task SearchAsync_EntraCannotIssueTheToken_AnswersUnavailable()
    {
        var headers = new StubAuthorizationHeaders(new MsalServiceException("temporarily_unavailable", "Entra is unavailable."));

        var result = await Directory(new StubGraph(_ => throw new InvalidOperationException("Graph is never asked.")), headers).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task SearchAsync_GraphCannotBeReached_AnswersUnavailable()
    {
        var graph = new StubGraph(_ => throw new HttpRequestException("No such host is known."));

        var result = await Directory(graph).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
        Assert.Equal(LogLevel.Warning, Assert.Single(_logger.Collector.GetSnapshot()).Level);
    }

    [Fact]
    public async Task SearchAsync_ResilienceHandlerGivesUpOnGraph_AnswersUnavailable()
    {
        var graph = new StubGraph(_ => throw new TimeoutRejectedException());

        var result = await Directory(graph).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task SearchAsync_HttpClientTimesOut_AnswersUnavailable()
    {
        var graph = new StubGraph(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));

        var result = await Directory(graph).SearchAsync(SearchText, TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task SearchAsync_RequestAbandoned_Throws()
    {
        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();
        var graph = new StubGraph(_ => throw new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Directory(graph).SearchAsync(SearchText, abandoned.Token));
    }

    [Fact]
    public async Task SearchAsync_NobodySignedIn_Throws()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        using var client = new HttpClient(new StubGraph(_ => throw new InvalidOperationException("Graph is never asked."))) { BaseAddress = MicrosoftGraph.BaseAddress };
        var directory = new GraphPeopleDirectory(client, new StubAuthorizationHeaders(), accessor, _logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.SearchAsync(SearchText, TestContext.Current.CancellationToken));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private GraphPeopleDirectory Directory(StubGraph graph, StubAuthorizationHeaders? headers = null)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = _person } };
#pragma warning disable CA2000 // The directory owns the client for the test, as the HTTP client factory's typed client does.
        var client = new HttpClient(graph) { BaseAddress = MicrosoftGraph.BaseAddress };
#pragma warning restore CA2000

        return new GraphPeopleDirectory(client, headers ?? new StubAuthorizationHeaders(), accessor, _logger);
    }

    private sealed record GraphRequest(Uri Address, string? ConsistencyLevel, string? Authorization);

    private sealed class StubGraph(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<GraphRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(new GraphRequest(
                request.RequestUri!,
                request.Headers.TryGetValues(MicrosoftGraph.ConsistencyLevelHeader, out var levels) ? string.Join(',', levels) : null,
                request.Headers.Authorization?.ToString()));

            return Task.FromResult(answer(request));
        }
    }

    private sealed class StubAuthorizationHeaders(Exception? refusal = null) : IAuthorizationHeaderProvider
    {
        public IReadOnlyList<string> Scopes { get; private set; } = [];

        public AuthorizationHeaderProviderOptions? Options { get; private set; }

        public ClaimsPrincipal? Principal { get; private set; }

        public Task<string> CreateAuthorizationHeaderForUserAsync(IEnumerable<string> scopes, AuthorizationHeaderProviderOptions? authorizationHeaderProviderOptions = null, ClaimsPrincipal? claimsPrincipal = null, CancellationToken cancellationToken = default)
        {
            Scopes = [.. scopes];
            Options = authorizationHeaderProviderOptions;
            Principal = claimsPrincipal;

            return refusal is null ? Task.FromResult(AuthorizationHeader) : Task.FromException<string>(refusal);
        }

        public Task<string> CreateAuthorizationHeaderForAppAsync(string scopes, AuthorizationHeaderProviderOptions? downstreamApiOptions = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The directory never asks for an application token.");

        public Task<string> CreateAuthorizationHeaderAsync(IEnumerable<string> scopes, AuthorizationHeaderProviderOptions? options = null, ClaimsPrincipal? claimsPrincipal = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The directory asks for a token for the user.");
    }
}
