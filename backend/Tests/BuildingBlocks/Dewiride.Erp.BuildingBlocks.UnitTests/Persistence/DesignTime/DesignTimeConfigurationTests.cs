using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.Persistence.DesignTime;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.DesignTime;

public sealed class DesignTimeConfigurationTests
{
    [Fact]
    public void Apply_AtDesignTime_ForcesTheLocalDevelopmentSourceOverAStoreEndpoint()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration[ErpConfigurationSourceResolver.EndpointVariable] = "https://example.azconfig.io";

        DesignTimeConfiguration.Apply(builder, isDesignTime: true);

        Assert.Equal(ErpConfigurationSourceResolver.LocalDevelopmentSource, builder.Configuration[ErpConfigurationSourceResolver.SourceSetting]);
        Assert.Equal(ErpConfigurationSource.LocalDevelopment, ErpConfigurationSourceResolver.Resolve(builder.Configuration, builder.Environment).Source);
    }

    [Fact]
    public void Apply_AtRunTime_LeavesTheSourceSettingUntouched()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });

        DesignTimeConfiguration.Apply(builder, isDesignTime: false);

        Assert.Null(builder.Configuration[ErpConfigurationSourceResolver.SourceSetting]);
    }
}
