using System.Net;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints.TestJobs;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Modules.Platform.Jobs.IntegrationTests.Runs.Endpoints;

public sealed class JobsFeatureGateTests(JobsFeatureGateTests.Fixture fixture) : IClassFixture<JobsFeatureGateTests.Fixture>
{
    private const string FeatureDisabled = "feature.disabled";

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/runs")]
    [InlineData("GET", "/runs/{id}")]
    [InlineData("POST", "/runs/{id}/rerun")]
    [InlineData("POST", "/runs/{id}/cancel")]
    public async Task Request_ModuleDisabled_AnswersFeatureDisabled(string method, string route)
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        using var request = new HttpRequestMessage(new HttpMethod(method), JobsApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await JobsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, FeatureDisabled);
        Assert.Contains(JobsModule.FeatureFlag, problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ModuleDisabled_RunsTheJobAllTheSame()
    {
        var id = await JobsApi.SeedAsync(fixture.Factory.Services, new JobRunSeed(TestJobDefinitions.Succeeding.Name, $"gated:{JobsApi.NewMarker()}", JobRunStatus.Pending));

        var outcome = await new JobRunnerDriver(fixture.Factory.Services).RunAsync(JobRunId.From(id), TestContext.Current.CancellationToken);

        Assert.Equal(JobRunStatus.Succeeded, outcome.Status);
        Assert.Null(outcome.ErrorCode);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        private readonly ErpApiFactory _root;

        public Fixture()
        {
            _root = new ErpApiFactory().WithFeature(JobsModule.FeatureFlag, enabled: false);
            Factory = _root.WithTestJobs();
        }

        public WebApplicationFactory<Program> Factory { get; }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await _root.DisposeAsync();
        }
    }
}
