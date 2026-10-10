using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Logging;
using Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Logging;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Observability;

public sealed class IdentityModelLoggingTests
{
    [Fact]
    public async Task Get_SignInStartAndRefusedTokenOnAStartedHost_LeaveTheBoundedLoggerInIdentityModel()
    {
        await using var factory = BearerTokenRoutes.Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var signIn = await client.GetAsync(new Uri($"{AuthPaths.Login}?returnUrl=%2F", UriKind.Relative), TestContext.Current.CancellationToken);
        using var refused = await BearerTokenRoutes.SendAsync(client, HttpMethod.Get, BearerTokenRoutes.Path, "not-a-token");

        Assert.Equal(HttpStatusCode.Found, signIn.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.IsType<BoundedIdentityModelLogger>(LogHelper.Logger);
    }
}
