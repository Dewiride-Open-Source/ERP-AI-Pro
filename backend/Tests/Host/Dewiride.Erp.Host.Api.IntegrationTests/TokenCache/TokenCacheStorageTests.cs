using System.Buffers.Binary;
using System.Net;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;
using Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;
using Dewiride.Erp.BuildingBlocks.Caching;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

public sealed class TokenCacheStorageTests
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task Post_SignIn_StoresTheEntryProtectedByTheKeyRingAndBoundedByTheSession()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        using var client = TestSignIn.CreateClient(factory);
        var before = TimeProvider.System.GetUtcNow();

        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);

        var after = TimeProvider.System.GetUtcNow();
        var row = await TokenCacheRow.FindAsync(factory.Deployment.TokenCacheKeyFor(TestUsers.Accountant));
        Assert.NotNull(row);
        Assert.NotEqual((byte)'{', row.Value[0]);
        Assert.False(Contains(row.Value, $"test-refresh-token.{TestUsers.Accountant.ObjectId:N}"));
        Assert.False(Contains(row.Value, $"test-access-token.{TestUsers.Accountant.ObjectId:N}"));
        var entry = Protector(factory.Services).Unprotect(row.Value);
        Assert.Equal((byte)'{', entry[0]);
        Assert.True(Contains(entry, $"test-refresh-token.{TestUsers.Accountant.ObjectId:N}"));
        Assert.Equal((long)(IdleTimeout + Margin).TotalSeconds, row.SlidingExpirationInSeconds);
        Assert.InRange(row.ExpiresAtTime, before + IdleTimeout + Margin - TimeSpan.FromSeconds(1), after + IdleTimeout + Margin);
        Assert.NotNull(row.AbsoluteExpiration);
        Assert.InRange(row.AbsoluteExpiration.Value, before + Lifetime + Margin - TimeSpan.FromSeconds(1), after + Lifetime + Margin);
    }

    [Fact]
    public async Task Post_Logout_RecordsTheSignOutUnderTheDeploymentPrefixUntilEverySessionIssuedBeforeItHasEnded()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        var before = TimeProvider.System.GetUtcNow();

        using var signOut = await client.PostAsync(new Uri(AuthPaths.Logout, UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        var after = TimeProvider.System.GetUtcNow();
        Assert.Equal(HttpStatusCode.Found, signOut.StatusCode);
        Assert.Null(await TokenCacheRow.FindAsync(SessionRevocations.KeyPrefix + TestUsers.Accountant.AccountId));
        var row = await TokenCacheRow.FindAsync(factory.Deployment.TokenCacheKeyPrefix + SessionRevocations.KeyPrefix + TestUsers.Accountant.AccountId);
        Assert.NotNull(row);
        Assert.InRange(new DateTimeOffset(BinaryPrimitives.ReadInt64BigEndian(row.Value), TimeSpan.Zero), before, after);
        Assert.Null(row.SlidingExpirationInSeconds);
        Assert.NotNull(row.AbsoluteExpiration);
        Assert.InRange(row.AbsoluteExpiration.Value, before + Lifetime + Margin - TimeSpan.FromSeconds(1), after + Lifetime + Margin);
    }

    [Fact]
    public async Task Get_WhenTheEntryWasWrittenUnderAnotherKeyRing_AnswersUnauthenticatedClearsTheCookieAndTheNextSignInSucceeds()
    {
        await using var root = new ErpApiFactory().WithTestEndpoints(SessionCookies.MapSignInAndObjectId);
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddFakeLogging()));
        using var client = TestSignIn.CreateClient(factory);
        using var signIn = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        await RewriteUnderAnotherKeyRingAsync(factory.Services, TestUsers.Accountant);

        using var refused = await SessionCookies.GetObjectIdAsync(client);
        using var signInAgain = await TestSignIn.SignInAsync(client, TestUsers.Accountant);
        using var signedIn = await SessionCookies.GetObjectIdAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Contains(SessionCookies.Cleared, SessionCookies.SetCookieOf(refused), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var warnings = factory.Services.GetRequiredService<FakeLogCollector>().GetSnapshot().Where(record => record.Category == typeof(ProtectedTokenCacheStore).FullName).ToList();
        Assert.NotEmpty(warnings);
        Assert.All(warnings, warning =>
        {
            Assert.Equal(LogLevel.Warning, warning.Level);
            Assert.DoesNotContain(TestUsers.Accountant.AccountId, warning.Message, StringComparison.Ordinal);
        });
    }

    private static IDataProtector Protector(IServiceProvider services) =>
        services.GetRequiredService<IDataProtectionProvider>().CreateProtector(ProtectedTokenCacheStore.Purpose);

    // The plaintext entry protected by an unrelated key ring under the same application name and purpose, as an instance
    // that lost its key ring would have written it.
    private static async Task RewriteUnderAnotherKeyRingAsync(IServiceProvider services, TestUser user)
    {
        var store = services.GetRequiredKeyedService<IDistributedCache>(CachingRegistration.SqlServerCacheKey);
        var stored = await store.GetAsync(user.AccountId, TestContext.Current.CancellationToken) ?? throw new InvalidOperationException($"No token cache entry exists for {user.UserName}.");
        var foreign = new EphemeralDataProtectionProvider().CreateProtector(DataProtectionKeyRing.ApplicationName, ProtectedTokenCacheStore.Purpose).Protect(Protector(services).Unprotect(stored));
        await store.SetAsync(user.AccountId, foreign, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }, TestContext.Current.CancellationToken);
    }

    private static bool Contains(byte[] value, string text) => value.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0;
}
