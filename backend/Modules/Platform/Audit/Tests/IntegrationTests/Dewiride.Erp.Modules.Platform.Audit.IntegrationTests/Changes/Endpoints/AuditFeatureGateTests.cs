using System.Net;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints;

public sealed class AuditFeatureGateTests(AuditFeatureGateTests.Fixture fixture) : IClassFixture<AuditFeatureGateTests.Fixture>
{
    private const string FeatureDisabled = "feature.disabled";

    [Theory]
    [InlineData("")]
    [InlineData("/export")]
    public async Task Get_ModuleDisabled_AnswersFeatureDisabled(string suffix)
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(ChangesApi.Path(suffix), TestContext.Current.CancellationToken);

        var problem = await ChangesApi.AssertProblemAsync(response, HttpStatusCode.NotFound, FeatureDisabled);
        Assert.Contains(AuditModule.FeatureFlag, problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateDepartment_ModuleDisabled_RecordsTheChangeAllTheSame()
    {
        using var client = fixture.Factory.CreateClient().AsUser(TestUsers.Administrator);

        var department = await ChangesApi.CreateDepartmentAsync(client, ChangesApi.UniqueName("Compliance"));

        Assert.Equal([(ChangeOperation.Created, ChangesApi.DepartmentKey)], await AuditTables.ChangeRecordsOfAsync(department.Id));
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = new ErpApiFactory().WithFeature(AuditModule.FeatureFlag, enabled: false);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
