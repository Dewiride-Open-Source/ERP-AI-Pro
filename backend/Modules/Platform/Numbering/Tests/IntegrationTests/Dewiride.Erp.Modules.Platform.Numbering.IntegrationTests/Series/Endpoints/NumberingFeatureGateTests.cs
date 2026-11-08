using System.Net;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Numbering;

namespace Dewiride.Erp.Modules.Platform.Numbering.IntegrationTests.Series.Endpoints;

// The flag gates the routes alone: with the module off, a module that issues documents still numbers them.
public sealed class NumberingFeatureGateTests(NumberingFeatureGateTests.Fixture fixture) : IClassFixture<NumberingFeatureGateTests.Fixture>
{
    [Theory]
    [InlineData("GET", "/documents")]
    [InlineData("GET", "/series/{id}")]
    [InlineData("POST", "/series")]
    [InlineData("PUT", "/series/{id}")]
    public async Task Request_ModuleDisabled_AnswersFeatureDisabled(string method, string route)
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        using var request = new HttpRequestMessage(new HttpMethod(method), NumberingApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await NumberingApi.AssertProblemAsync(response, HttpStatusCode.NotFound, "feature.disabled");
        Assert.Contains(NumberingModule.FeatureFlag, problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NextAsync_ModuleDisabled_StillNumbersADocument()
    {
        var document = TestNumberedDocuments.Numbered(16);
        var cancellationToken = TestContext.Current.CancellationToken;
        await NumberingResets.ResetAsync(fixture.Factory.Services, document.Key, cancellationToken);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var setUp = await scope.ServiceProvider.GetRequiredService<INumberingAdministration>().SetUpAsync(document.Key, new NumberFormat("TG", NumberYearStyle.None, "", 1), 1, cancellationToken);
            Assert.True(setUp.IsSuccess, setUp.Error?.Message);
        }

        var issued = await NumberIssuer.IssueAsync(fixture.Factory.Services, document.Key, 1, cancellationToken);

        Assert.Equal("TG1", Assert.Single(issued.Value).Text);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithFeature(NumberingModule.FeatureFlag, enabled: false).WithTestServices(TestNumberedDocuments.Register);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
