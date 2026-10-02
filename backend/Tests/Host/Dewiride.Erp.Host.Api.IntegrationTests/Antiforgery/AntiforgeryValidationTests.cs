using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Authentication.Antiforgery;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Idempotency.Http;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Antiforgery;

public sealed class AntiforgeryValidationTests(AntiforgeryValidationTests.Fixture fixture) : IClassFixture<AntiforgeryValidationTests.Fixture>
{
    private const string IdempotentPath = "/__test/idempotent-changes";

    private const string AttachmentsPath = "/api/platform/attachments";

    private const string AttachmentsFlag = "Erp.Modules.Platform.Attachments";

    public static TheoryData<string> DataChangingMethods => ["POST", "PUT", "PATCH", "DELETE"];

    public static TheoryData<string> SafeMethods => ["GET", "HEAD", "OPTIONS", "TRACE"];

    [Theory]
    [MemberData(nameof(DataChangingMethods))]
    public async Task Send_SignedInWithTheSessionCookieAndTheRequestToken_ReachesTheEndpoint(string method)
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await SendAsync(client, new HttpMethod(method), TokenCookies.ChangesPath);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(DataChangingMethods))]
    public async Task Send_SignedInWithTheSessionCookieWithoutTheRequestToken_AnswersTokenMissing(string method)
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await SendAsync(client, new HttpMethod(method), TokenCookies.ChangesPath);

        await AssertRefusedAsync(response, AntiforgeryProblems.TokenMissing);
    }

    [Theory]
    [MemberData(nameof(SafeMethods))]
    public async Task Send_SafeMethodSignedInWithoutTheRequestToken_ReachesTheEndpoint(string method)
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await SendAsync(client, new HttpMethod(method), TokenCookies.ChangesPath);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_SignedInWithARequestTokenTheApiNeverIssued_AnswersTokenInvalid()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await SendAsync(client, HttpMethod.Post, TokenCookies.ChangesPath, requestToken: "not-a-token-the-api-issued");

        await AssertRefusedAsync(response, "antiforgery.token-invalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_TokenOnlyInAFormFieldOfAnEndpointThatReadsNoForm_AnswersTokenMissing(bool multipart)
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var token = TokenCookies.RequestTokenOf(signIn);
        using HttpContent form = multipart
            ? new MultipartFormDataContent { { new StringContent(token), AntiforgeryTokens.FormFieldName } }
            : new FormUrlEncodedContent([KeyValuePair.Create(AntiforgeryTokens.FormFieldName, token)]);

        using var response = await client.PostAsync(new Uri(TokenCookies.ChangesPath, UriKind.Relative), form, TestContext.Current.CancellationToken);

        await AssertRefusedAsync(response, AntiforgeryProblems.TokenMissing);
    }

    [Fact]
    public async Task Post_UploadWithTheTokenOnlyInAFormPart_IsRefusedAndStoresNothing()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var fileName = $"forged-form-token-{Guid.CreateVersion7():N}.txt";
        using var file = new ByteArrayContent("an agreement the person signed"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(TokenCookies.RequestTokenOf(signIn)), AntiforgeryTokens.FormFieldName },
            { file, "file", fileName },
        };

        using var response = await client.PostAsync(new Uri(AttachmentsPath, UriKind.Relative), form, TestContext.Current.CancellationToken);

        await AssertRefusedAsync(response, AntiforgeryProblems.TokenMissing);
        Assert.Equal(0, await CountStoredAsync(client, fileName));
    }

    [Fact]
    public async Task Post_SignedInWithTheRequestTokenButWithoutTheCookieToken_AnswersTokenMissing()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var session = TokenCookies.CookieHeader((TokenCookies.SessionCookieName, TokenCookies.ValueOf(signIn, TokenCookies.SessionCookieName)));

        using var response = await TokenCookies.SendWithAsync(fixture.Factory, HttpMethod.Post, TokenCookies.ChangesPath, session, TokenCookies.RequestTokenOf(signIn));

        await AssertRefusedAsync(response, AntiforgeryProblems.TokenMissing);
    }

    [Fact]
    public async Task Post_WithTheRequestTokenOfAnotherBrowserOfThePerson_AnswersTokenInvalid()
    {
        using var laptop = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var laptopSignIn = await TestSignIn.SignInAsync(laptop, TestUsers.Accountant);
        using var phone = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var phoneSignIn = await TestSignIn.SignInAsync(phone, TestUsers.Accountant);

        using var response = await SendAsync(laptop, HttpMethod.Post, TokenCookies.ChangesPath, requestToken: TokenCookies.RequestTokenOf(phoneSignIn));

        await AssertRefusedAsync(response, AntiforgeryProblems.TokenInvalid);
    }

    [Fact]
    public async Task Post_WithTheRequestTokenOfThePersonWhoSignedInBeforeOnThatBrowser_AnswersTokenInvalid()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var accountantSignIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var accountantToken = TokenCookies.RequestTokenOf(accountantSignIn);
        using var administratorSignIn = await TestSignIn.SignInAsync(client, TestUsers.Administrator);

        using var stale = await SendAsync(client, HttpMethod.Post, TokenCookies.ChangesPath, requestToken: accountantToken);
        using var current = await SendAsync(client, HttpMethod.Post, TokenCookies.ChangesPath);

        Assert.Null(TokenCookies.SetCookieOf(administratorSignIn, AntiforgeryTokens.CookieName));
        await AssertRefusedAsync(stale, AntiforgeryProblems.TokenInvalid);
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
    }

    [Fact]
    public async Task Post_AnonymouslyWithoutARequestToken_ReachesAnAnonymousEndpoint()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);

        using var response = await SendAsync(client, HttpMethod.Post, TokenCookies.ChangesPath);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_SignedInThroughAHeaderWithoutARequestToken_ReachesTheEndpoint()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await SendAsync(client, HttpMethod.Post, TokenCookies.ProtectedChangesPath);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_AnonymouslyToAProtectedEndpointWithoutARequestToken_AnswersUnauthenticatedBeforeTheCheck()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);

        using var response = await SendAsync(client, HttpMethod.Post, TokenCookies.ProtectedChangesPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemTypes.RequestUnauthenticated, await CodeOfAsync(response));
    }

    [Fact]
    public async Task Post_IdempotentEndpointRefusedForItsToken_ClaimsNoKey()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var key = Guid.CreateVersion7().ToString("D");

        using var refused = await SendAsync(client, HttpMethod.Post, IdempotentPath, idempotencyKey: key);
        using var accepted = await SendAsync(client, HttpMethod.Post, IdempotentPath, requestToken: TokenCookies.RequestTokenOf(signIn), idempotencyKey: key);

        await AssertRefusedAsync(refused, AntiforgeryProblems.TokenMissing);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.False(accepted.Headers.Contains(IdempotencyKeyHeader.ReplayedName));
    }

    [Fact]
    public async Task Post_DisabledModuleSignedInWithoutTheRequestToken_AnswersTokenMissingBeforeTheFeatureGate()
    {
        await using var factory = new ErpApiFactory().WithFeature(AttachmentsFlag, enabled: false).WithTestEndpoints(TestSignIn.Map);
        using var client = TestSignIn.CreateClientWithoutRequestToken(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var downloadLinks = $"{AttachmentsPath}/{Guid.CreateVersion7():D}/download-links";

        using var refused = await SendAsync(client, HttpMethod.Post, downloadLinks);
        using var gated = await SendAsync(client, HttpMethod.Post, downloadLinks, requestToken: TokenCookies.RequestTokenOf(signIn));

        await AssertRefusedAsync(refused, AntiforgeryProblems.TokenMissing);
        Assert.Equal(HttpStatusCode.NotFound, gated.StatusCode);
        Assert.Equal("feature.disabled", await CodeOfAsync(gated));
    }

    [Fact]
    public async Task Post_UploadSignedInWithoutTheRequestToken_IsRefusedAndStoresNothing()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var fileName = $"forged-{Guid.CreateVersion7():N}.txt";

        using var response = await UploadAsync(client, fileName);

        await AssertRefusedAsync(response, AntiforgeryProblems.TokenMissing);
        Assert.Equal(0, await CountStoredAsync(client, fileName));
    }

    [Fact]
    public async Task Post_UploadSignedInWithTheRequestToken_StoresTheFile()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var fileName = $"accepted-{Guid.CreateVersion7():N}.txt";

        using var response = await UploadAsync(client, fileName);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await CountStoredAsync(client, fileName));
    }

    [Fact]
    public async Task Post_Refused_AnswersAProblemNamingTheCodeAndTheRequest()
    {
        using var client = TestSignIn.CreateClientWithoutRequestToken(fixture.Factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        using var response = await SendAsync(client, HttpMethod.Post, TokenCookies.ChangesPath);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = problem.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("/problems/antiforgery.token-missing", root.GetProperty("type").GetString());
        Assert.Equal("antiforgery.token-missing", root.GetProperty("code").GetString());
        Assert.Equal(AntiforgeryProblems.Title, root.GetProperty("title").GetString());
        Assert.Equal(AntiforgeryProblems.TokenMissingDetail, root.GetProperty("detail").GetString());
        Assert.Equal(TokenCookies.ChangesPath, root.GetProperty("instance").GetString());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), root.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Post_Refused_LogsTheCodeWithoutAnyToken()
    {
        await using var root = new ErpApiFactory().WithTestEndpoints(TokenCookies.MapSignInAndChanges);
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddFakeLogging()));
        using var client = TestSignIn.CreateClientWithoutRequestToken(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var issued = TokenCookies.RequestTokenOf(signIn);
        string[] carried = [issued, TokenCookies.ValueOf(signIn, AntiforgeryTokens.CookieName), TokenCookies.ValueOf(signIn, TokenCookies.SessionCookieName)];
        var logs = factory.Services.GetRequiredService<FakeLogCollector>();
        logs.Clear();

        using var response = await SendAsync(client, HttpMethod.Post, TokenCookies.ChangesPath, requestToken: issued + "x");

        await AssertRefusedAsync(response, AntiforgeryProblems.TokenInvalid);
        var record = Assert.Single(logs.GetSnapshot(), entry => entry.Category == typeof(AntiforgeryValidationMiddleware).FullName);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(AntiforgeryProblems.TokenInvalid, record.GetStructuredStateValue("Code"));
        Assert.Equal("POST", record.GetStructuredStateValue("Method"));
        Assert.Equal(TokenCookies.ChangesPath, record.GetStructuredStateValue("Path"));
        Assert.DoesNotContain(logs.GetSnapshot(), entry => carried.Any(value => Carries(entry, value)));
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? requestToken = null, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (requestToken is not null)
        {
            request.Headers.Add(AntiforgeryTokens.HeaderName, requestToken);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add(IdempotencyKeyHeader.Name, idempotencyKey);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName)
    {
        using var file = new ByteArrayContent("an agreement the person signed"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var form = new MultipartFormDataContent { { file, "file", fileName } };

        return await client.PostAsync(new Uri(AttachmentsPath, UriKind.Relative), form, TestContext.Current.CancellationToken);
    }

    private static async Task<int> CountStoredAsync(HttpClient client, string fileName)
    {
        using var response = await client.GetAsync(new Uri($"{AttachmentsPath}?filter={Uri.EscapeDataString($"fileName:contains:{fileName}")}", UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return page.RootElement.GetProperty("totalCount").GetInt32();
    }

    private static bool Carries(FakeLogRecord record, string value) =>
        record.Message.Contains(value, StringComparison.Ordinal)
        || (record.Exception?.ToString().Contains(value, StringComparison.Ordinal) ?? false)
        || (record.StructuredState?.Any(pair => pair.Value?.Contains(value, StringComparison.Ordinal) ?? false) ?? false);

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await CodeOfAsync(response));
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("code").GetString();
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithTestEndpoints(routes =>
        {
            TokenCookies.MapSignInAndChanges(routes);
            routes.MapPost(IdempotentPath, () => Results.Created()).RequireIdempotencyKey();
        });

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
