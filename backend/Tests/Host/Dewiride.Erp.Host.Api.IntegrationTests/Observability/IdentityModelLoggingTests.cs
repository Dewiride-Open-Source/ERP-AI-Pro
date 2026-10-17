using System.Net;
using Dewiride.Erp.BuildingBlocks.Authentication;
using Dewiride.Erp.BuildingBlocks.Authentication.Logging;
using Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;
using Dewiride.Erp.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Abstractions;
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

    [Fact]
    public async Task Log_AnErrorAfterItsHostStopped_ThrowsNothing()
    {
        ILogger<BoundedIdentityModelLogger> records;
        await using (var factory = new ErpApiFactory())
        {
            records = factory.Services.GetRequiredService<ILogger<BoundedIdentityModelLogger>>();
        }

        var logger = new BoundedIdentityModelLogger(records, TimeProvider.System);
        var failure = Record.Exception(() => logger.Log(new LogEntry { EventLogLevel = EventLogLevel.Error, Message = "IDX10222: Lifetime validation failed." }));

        Assert.Null(failure);
    }
}
