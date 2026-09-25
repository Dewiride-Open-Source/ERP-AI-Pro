using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.OpenApi;

namespace Dewiride.Erp.Host.Api.IntegrationTests.OpenApi;

public sealed class OpenApiSnapshotTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task Document_Always_MatchesTheCommittedSnapshot()
    {
        var current = await OpenApiSnapshot.SerializeAsync(factory.Services, TestContext.Current.CancellationToken);
        if (OpenApiSnapshot.UpdateRequested)
        {
            await OpenApiSnapshot.WriteAsync(current, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(OpenApiSnapshot.Path), $"'{OpenApiSnapshot.Path}' does not exist. Create it with: {OpenApiSnapshot.RefreshCommand}");
        var committed = await OpenApiSnapshot.ReadAsync(TestContext.Current.CancellationToken);
        Assert.True(string.Equals(committed, current, StringComparison.Ordinal), OpenApiSnapshot.Describe(committed, current));
    }
}
