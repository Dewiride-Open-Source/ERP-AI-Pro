using System.Net;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints.Export;

public sealed class ExportConcurrencyTests
{
    [Fact]
    public async Task Get_WhileTheCappedNumberOfExportsIsInFlight_AnswersTooManyRequestsWithRetryAfter()
    {
        var history = new HeldChangeHistory(ChangeEndpoints.MaxConcurrentExports);
        using var root = new ErpApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IChangeHistory>(history)));
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        var inFlight = Enumerable.Range(0, ChangeEndpoints.MaxConcurrentExports)
            .Select(_ => client.GetAsync(ChangesApi.ExportPath(string.Empty), TestContext.Current.CancellationToken))
            .ToList();
        await history.Entered.WaitAsync(TestContext.Current.CancellationToken);

        using var refused = await client.GetAsync(ChangesApi.ExportPath(string.Empty), TestContext.Current.CancellationToken);

        await ChangesApi.AssertProblemAsync(refused, HttpStatusCode.TooManyRequests, "rate-limit.exceeded");
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        history.Release();
        foreach (var response in await Task.WhenAll(inFlight))
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Get_ExportsOneAfterAnotherBeyondTheCap_AreEachAccepted()
    {
        var history = new HeldChangeHistory(ChangeEndpoints.MaxConcurrentExports);
        history.Release();
        using var root = new ErpApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IChangeHistory>(history)));
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        for (var export = 0; export <= ChangeEndpoints.MaxConcurrentExports; export++)
        {
            using var response = await client.GetAsync(ChangesApi.ExportPath(string.Empty), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
