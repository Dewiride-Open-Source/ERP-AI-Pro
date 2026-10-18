using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Identity.Client;
using Polly.Timeout;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

public sealed partial class GraphDirectoryAccountsTests
{
    private static readonly Guid EnabledAccount = new("1f0e2d3c-4b5a-4968-8776-655443322110");

    private static readonly Guid DisabledAccount = new("2a1b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d");

    private static readonly Guid DeletedAccount = new("3b2c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e");

    private readonly FakeLogger<GraphDirectoryAccounts> _logger = new();

    [Fact]
    public async Task StatesOfAsync_IdsOfOneRequest_AsksGraphWithTheApplicationsTokenAndAnswersTheStateOfEveryId()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, Page(Account(EnabledAccount, true), Account(DisabledAccount, false))));
        var headers = new StubApplicationHeaders();

        var result = await Accounts(graph, headers).StatesOfAsync([EnabledAccount, DisabledAccount, DeletedAccount], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal(DirectoryAccountState.Enabled, result.Value[EnabledAccount]);
        Assert.Equal(DirectoryAccountState.Disabled, result.Value[DisabledAccount]);
        Assert.Equal(DirectoryAccountState.Deleted, result.Value[DeletedAccount]);
        var request = Assert.Single(graph.Requests);
        Assert.Equal(new Uri(MicrosoftGraph.BaseAddress, "users").AbsolutePath, request.Address.AbsolutePath);
        var query = QueryHelpers.ParseQuery(request.Address.Query);
        Assert.Equal($"id in ('{EnabledAccount:D}','{DisabledAccount:D}','{DeletedAccount:D}')", query["$filter"].ToString());
        Assert.Equal("id,accountEnabled", query["$select"].ToString());
        Assert.Null(request.ConsistencyLevel);
        Assert.Equal(StubApplicationHeaders.AuthorizationHeader, request.Authorization);
        Assert.Equal(MicrosoftGraph.ApplicationScope, headers.Scope);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, headers.Options?.AcquireTokenOptions.AuthenticationOptionsName);
    }

    [Fact]
    public async Task StatesOfAsync_MoreIdsThanOneRequestTakes_AsksForThemFifteenAtATimeWithOneToken()
    {
        var ids = Enumerable.Range(0, 31).Select(_ => Guid.CreateVersion7()).ToList();
        var graph = new StubGraph(request => Json(HttpStatusCode.OK, Page([.. IdsOf(request).Select(id => Account(id, true))])));
        var headers = new StubApplicationHeaders();

        var result = await Accounts(graph, headers).StatesOfAsync(ids, TestContext.Current.CancellationToken);

        Assert.Equal([15, 15, 1], graph.Requests.Select(request => IdsOf(request.Address).Count));
        Assert.Equal(ids, graph.Requests.SelectMany(request => IdsOf(request.Address)));
        Assert.Equal(1, headers.Requests);
        Assert.All(ids, id => Assert.Equal(DirectoryAccountState.Enabled, result.Value[id]));
    }

    [Fact]
    public async Task StatesOfAsync_TheSameIdTwice_AsksForItOnce()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, Page(Account(EnabledAccount, true))));

        var result = await Accounts(graph).StatesOfAsync([EnabledAccount, EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal([EnabledAccount], IdsOf(Assert.Single(graph.Requests).Address));
        Assert.Equal(DirectoryAccountState.Enabled, Assert.Single(result.Value).Value);
    }

    [Fact]
    public async Task StatesOfAsync_NoIds_AsksNeitherEntraNorGraph()
    {
        var graph = new StubGraph(_ => throw new InvalidOperationException("Graph is never asked."));
        var headers = new StubApplicationHeaders();

        var result = await Accounts(graph, headers).StatesOfAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result.Value);
        Assert.Equal(0, headers.Requests);
    }

    [Fact]
    public async Task StatesOfAsync_AnswerWithANextPageOnGraph_FollowsItWithTheSameToken()
    {
        const string NextLink = "https://graph.microsoft.com/v1.0/users?$skiptoken=next-page";
        var graph = new StubGraph(request => request.RequestUri!.AbsoluteUri == NextLink
            ? Json(HttpStatusCode.OK, Page(Account(DisabledAccount, false)))
            : Json(HttpStatusCode.OK, PageWithNext(NextLink, Account(EnabledAccount, true))));

        var result = await Accounts(graph).StatesOfAsync([EnabledAccount, DisabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(2, graph.Requests.Count);
        Assert.Equal(NextLink, graph.Requests[1].Address.AbsoluteUri);
        Assert.Equal(StubApplicationHeaders.AuthorizationHeader, graph.Requests[1].Authorization);
        Assert.Equal(DirectoryAccountState.Enabled, result.Value[EnabledAccount]);
        Assert.Equal(DirectoryAccountState.Disabled, result.Value[DisabledAccount]);
    }

    [Theory]
    [InlineData("https://graph.microsoft.com.example.com/v1.0/users?$skiptoken=next-page")]
    [InlineData("http://graph.microsoft.com/v1.0/users?$skiptoken=next-page")]
    [InlineData("https://graph.microsoft.com:8443/v1.0/users?$skiptoken=next-page")]
    [InlineData("https://someone@graph.microsoft.com/v1.0/users?$skiptoken=next-page")]
    [InlineData("/v1.0/users?$skiptoken=next-page")]
    public async Task StatesOfAsync_NextPageOutsideGraph_ThrowsWithoutSendingTheTokenThere(string nextLink)
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, PageWithNext(nextLink, Account(EnabledAccount, true))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Accounts(graph).StatesOfAsync([EnabledAccount, DisabledAccount], TestContext.Current.CancellationToken));

        Assert.Single(graph.Requests);
    }

    [Fact]
    public async Task StatesOfAsync_AnswerOfMorePagesThanARequestCanAskIds_Throws()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, PageWithNext("https://graph.microsoft.com/v1.0/users?$skiptoken=again")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken));

        Assert.Equal(GraphDirectoryAccounts.MaxIdsPerRequest, graph.Requests.Count);
    }

    [Fact]
    public async Task StatesOfAsync_AnswerNamingAnAccountItWasNotAskedAbout_Throws()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, Page(Account(EnabledAccount, true), Account(Guid.CreateVersion7(), true))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StatesOfAsync_AccountAnsweredWithoutWhetherItIsEnabled_Throws()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.OK, Page(Account(EnabledAccount, null))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken));
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
    public async Task StatesOfAsync_GraphRefusesOrFails_AnswersTheErrorAndLogsTheGraphCodeWithoutAnId(HttpStatusCode status, string code)
    {
        var graph = new StubGraph(_ => Json(status, $$$"""{"error":{"code":"Sample_Code","message":"The account {{{EnabledAccount:D}}} could not be read."}}"""));

        var result = await Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(code, result.Error?.Code);
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("Sample_Code", record.Message, StringComparison.Ordinal);
        Assert.Contains(((int)status).ToString(CultureInfo.InvariantCulture), record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(EnabledAccount.ToString("D"), record.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatesOfAsync_GraphRefusesTheApplication_AnswersTheRefusalOfTheApplicationsPermission()
    {
        var graph = new StubGraph(_ => Json(HttpStatusCode.Forbidden, """{"error":{"code":"Authorization_RequestDenied"}}"""));

        var result = await Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.AccountsAccessDenied, result.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task StatesOfAsync_AnswerTheErpCannotHaveCaused_ThrowsWithoutAnId(HttpStatusCode status)
    {
        var graph = new StubGraph(_ => Json(status, """{"error":{"code":"Request_UnsupportedQuery"}}"""));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken));

        Assert.Contains("Request_UnsupportedQuery", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(EnabledAccount.ToString("D"), thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(400, "unauthorized_client")]
    [InlineData(401, "invalid_client")]
    [InlineData(403, "access_denied")]
    public async Task StatesOfAsync_EntraRefusesTheApplicationsToken_AnswersAccessDeniedWithoutAskingGraph(int status, string errorCode)
    {
        var graph = new StubGraph(_ => throw new InvalidOperationException("Graph is never asked."));
        var headers = new StubApplicationHeaders(new MsalServiceException(errorCode, "AADSTS7000215: Invalid client secret provided.", status));

        var result = await Accounts(graph, headers).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.AccountsAccessDenied, result.Error);
        Assert.Empty(graph.Requests);
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Contains(errorCode, record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("AADSTS7000215", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatesOfAsync_EntraCannotIssueTheToken_AnswersUnavailable()
    {
        var headers = new StubApplicationHeaders(new MsalServiceException("temporarily_unavailable", "Entra is unavailable.", 503));

        var result = await Accounts(new StubGraph(_ => throw new InvalidOperationException("Graph is never asked.")), headers).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task StatesOfAsync_EntraThrottlesTheApplication_AnswersUnavailable()
    {
        var headers = new StubApplicationHeaders(new MsalThrottledServiceException(new MsalServiceException("throttled", "Too many requests.", 400)));

        var result = await Accounts(new StubGraph(_ => throw new InvalidOperationException("Graph is never asked.")), headers).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task StatesOfAsync_MsalCannotBuildTheRequest_AnswersUnavailable()
    {
        var headers = new StubApplicationHeaders(new MsalClientException("certificate_unavailable", "The certificate could not be loaded."));

        var result = await Accounts(new StubGraph(_ => throw new InvalidOperationException("Graph is never asked.")), headers).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task StatesOfAsync_EntraCannotBeReached_AnswersUnavailable()
    {
        var headers = new StubApplicationHeaders(new HttpRequestException("No such host is known."));

        var result = await Accounts(new StubGraph(_ => throw new InvalidOperationException("Graph is never asked.")), headers).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
        Assert.Equal(LogLevel.Warning, Assert.Single(_logger.Collector.GetSnapshot()).Level);
    }

    [Fact]
    public async Task StatesOfAsync_GraphCannotBeReached_AnswersUnavailable()
    {
        var graph = new StubGraph(_ => throw new HttpRequestException("No such host is known."));

        var result = await Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task StatesOfAsync_ResilienceHandlerGivesUpOnGraph_AnswersUnavailable()
    {
        var graph = new StubGraph(_ => throw new TimeoutRejectedException());

        var result = await Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task StatesOfAsync_HttpClientTimesOut_AnswersUnavailable()
    {
        var graph = new StubGraph(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));

        var result = await Accounts(graph).StatesOfAsync([EnabledAccount], TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryErrors.Unavailable, result.Error);
    }

    [Fact]
    public async Task StatesOfAsync_RequestAbandoned_Throws()
    {
        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();
        var graph = new StubGraph(_ => throw new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Accounts(graph).StatesOfAsync([EnabledAccount], abandoned.Token));
    }

    [Fact]
    public void FilterPath_Ids_QuotesEachInsideTheInFilterAndSelectsTheAccountState()
    {
        var path = GraphDirectoryAccounts.FilterPath([EnabledAccount, DisabledAccount]);

        var query = QueryHelpers.ParseQuery(new Uri(MicrosoftGraph.BaseAddress, path).Query);
        Assert.StartsWith("users?", path, StringComparison.Ordinal);
        Assert.Equal($"id in ('{EnabledAccount:D}','{DisabledAccount:D}')", query["$filter"].ToString());
        Assert.Equal("id,accountEnabled", query["$select"].ToString());
    }

    private static string Account(Guid id, bool? enabled) =>
        enabled is { } isEnabled
            ? $$"""{"id":"{{id:D}}","accountEnabled":{{(isEnabled ? "true" : "false")}}}"""
            : $$"""{"id":"{{id:D}}"}""";

    private static string Page(params string[] accounts) => $$"""{"value":[{{string.Join(',', accounts)}}]}""";

    private static string PageWithNext(string nextLink, params string[] accounts) => $$"""{"value":[{{string.Join(',', accounts)}}],"@odata.nextLink":"{{nextLink}}"}""";

    private static List<Guid> IdsOf(HttpRequestMessage request) => IdsOf(request.RequestUri!);

    private static List<Guid> IdsOf(Uri address) =>
        [.. QuotedId().Matches(QueryHelpers.ParseQuery(address.Query)["$filter"].ToString()).Select(match => Guid.Parse(match.Groups[1].Value))];

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [GeneratedRegex("'([0-9a-f-]{36})'")]
    private static partial Regex QuotedId();

    private GraphDirectoryAccounts Accounts(StubGraph graph, StubApplicationHeaders? headers = null)
    {
#pragma warning disable CA2000 // The directory owns the client for the test, as the HTTP client factory's typed client does.
        var client = new HttpClient(graph) { BaseAddress = MicrosoftGraph.BaseAddress };
#pragma warning restore CA2000

        return new GraphDirectoryAccounts(client, headers ?? new StubApplicationHeaders(), _logger);
    }
}
