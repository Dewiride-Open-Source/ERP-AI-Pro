using Dewiride.Erp.BuildingBlocks.Caching;
using Dewiride.Erp.Testing;
using Microsoft.Extensions.Caching.Distributed;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Caching;

public sealed class DistributedCacheRegistrationTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public void Services_Api_RegisterNoUnkeyedDistributedCacheSoHybridCacheStaysInProcess()
    {
        Assert.Null(factory.Services.GetService<IDistributedCache>());
    }

    [Fact]
    public void Services_Api_RegisterTheSqlServerCacheUnderItsKey()
    {
        Assert.NotNull(factory.Services.GetRequiredKeyedService<IDistributedCache>(CachingRegistration.SqlServerCacheKey));
    }
}
