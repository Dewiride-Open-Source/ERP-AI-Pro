using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Deployment;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

public sealed class TokenCacheLoggingTests
{
    private const string IdentityWebCategory = "Microsoft.Identity.Web";

    private const string TokenCacheCategory = "Microsoft.Identity.Web.TokenCacheProviders";

    [Fact]
    public async Task SignInSessionOutageAndSignOut_WhenConfigurationLogsEveryLevelOfTheIdentityLibraries_WriteNoRecordContainingTheAccountIdOrAToken()
    {
        var outage = new TokenCacheOutage();
        await using var root = new ErpApiFactory()
            .WithConfiguration($"Logging:LogLevel:{IdentityWebCategory}", "Trace")
            .WithConfiguration($"Logging:LogLevel:{TokenCacheCategory}", "Trace")
            .WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddFakeLogging();
            services.DecorateTokenCacheStore(outage.Wrap);
        }));
        using var client = TestSignIn.CreateClient(factory);

        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        using var signedIn = await SessionCookies.GetObjectIdAsync(client);
        outage.Begin(TokenCacheOperations.All, TestUsers.Accountant.AccountId);
        using var failed = await SessionCookies.GetObjectIdAsync(client);
        outage.End();
        using var signOut = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(HttpStatusCode.Found, signOut.StatusCode);
        var records = factory.Services.GetRequiredService<FakeLogCollector>().GetSnapshot();
        Assert.Contains(records, record => record.Category == typeof(MsalDistributedTokenCacheAdapter).FullName && record.Level == LogLevel.Error);
        Assert.DoesNotContain(records, record => record.Category == typeof(MsalDistributedTokenCacheAdapter).FullName && record.Level < LogLevel.Error);
        var tokens = factory.Services.GetRequiredService<TestTokenEndpoint>().IssuedTokens;
        Assert.Equal(3, tokens.Count);
        string[] secrets = [TestUsers.Accountant.AccountId, .. tokens];
        var leaks = records
            .SelectMany(record => secrets.Where(secret => Carries(record, secret)).Select(secret => $"{record.Category} ({record.Level}) carries {Describe(secret)}"))
            .ToList();
        Assert.Empty(leaks);
    }

    private static bool Carries(FakeLogRecord record, string secret) =>
        record.Message.Contains(secret, StringComparison.Ordinal)
        || (record.Exception?.ToString().Contains(secret, StringComparison.Ordinal) ?? false)
        || (record.StructuredState?.Any(pair => pair.Value?.Contains(secret, StringComparison.Ordinal) ?? false) ?? false);

    private static string Describe(string secret) =>
        string.Equals(secret, TestUsers.Accountant.AccountId, StringComparison.Ordinal) ? "the account id" : "an issued token";
}
