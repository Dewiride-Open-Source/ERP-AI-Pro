using System.Net;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Shared;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Approvals;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.Approvals.IntegrationTests.Routes;

// The flag gates the routes alone: with the module off, a module still asks for approval of its records, routed as ever, while
// nobody can decide a request.
public sealed class ApprovalsFeatureGateTests(ApprovalsFeatureGateTests.Fixture fixture) : IClassFixture<ApprovalsFeatureGateTests.Fixture>
{
    [Theory]
    [MemberData(nameof(ApprovalsPermissionTests.EveryRoute), MemberType = typeof(ApprovalsPermissionTests))]
    public async Task Request_ModuleDisabled_AnswersFeatureDisabled(string method, string route)
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);
        using var request = new HttpRequestMessage(new HttpMethod(method), ApprovalsApi.RouteFor(route));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await ApprovalsApi.AssertProblemAsync(response, HttpStatusCode.NotFound, "feature.disabled");
        Assert.Contains(ApprovalsModule.FeatureFlag, problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitAsync_ModuleDisabled_StillAsksForApproval()
    {
        var asker = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;

        var submitted = await ApprovalRequestSeeder.SubmitAsync(fixture.Factory.Services, new ApprovalRequestSeed(TestApprovalKinds.Api(7).Key, asker), cancellationToken);

        Assert.Equal(ApprovalState.Submitted, submitted.Value.State);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var request = await scope.ServiceProvider.GetRequiredService<IApprovalInbox>().GetAsync(Assert.NotNull(submitted.Value.RequestId), new ApprovalCaller(asker, new HashSet<ErpRole>(), SeesAll: false, DecidesAny: false), cancellationToken);
        Assert.Equal((ApprovalState.Submitted, (ErpRole?)ErpRole.Administrator), (request.Value.Request.State, request.Value.Request.WaitingForRole));
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithFeature(ApprovalsModule.FeatureFlag, enabled: false).WithTestServices(TestApprovalKinds.Register);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
