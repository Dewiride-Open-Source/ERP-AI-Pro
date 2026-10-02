using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Caching;
using Dewiride.Erp.BuildingBlocks.Caching.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Catalog;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Caching;

public sealed class CachingRegistrationTests
{
    [Fact]
    public void AddErpCaching_Configuration_BindsTheHybridCacheDefaults()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            [$"{CachingOptions.SectionName}:DefaultExpiration"] = "00:02:00",
            [$"{CachingOptions.SectionName}:MaximumPayloadBytes"] = "4096",
        });

        var hybrid = provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value;

        Assert.Equal(4096, hybrid.MaximumPayloadBytes);
        Assert.Equal(TimeSpan.FromMinutes(2), hybrid.DefaultEntryOptions?.Expiration);
        Assert.Equal(TimeSpan.FromMinutes(2), hybrid.DefaultEntryOptions?.LocalCacheExpiration);
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentCallersOfOneKey_ShareOneFactoryExecution()
    {
        await using var provider = Build([]);
        var cache = provider.GetRequiredService<HybridCache>();
        var executions = 0;
        using var gate = new SemaphoreSlim(0);

        var callers = Enumerable.Range(0, 8)
            .Select(_ => cache.GetOrCreateAsync("reference:states", async cancellationToken =>
            {
                Interlocked.Increment(ref executions);
                await gate.WaitAsync(cancellationToken);
                return "IN-KA";
            }, cancellationToken: TestContext.Current.CancellationToken).AsTask())
            .ToList();
        gate.Release();
        var values = await Task.WhenAll(callers);

        Assert.Equal(1, executions);
        Assert.All(values, value => Assert.Equal("IN-KA", value));
    }

    [Fact]
    public void AddErpCaching_SqlServerCacheOptions_PointAtTheCacheTableWithTheDatabaseTimeoutAndTheSystemClock()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Server=localhost;Database=ErpAiPro;Integrated Security=True;Encrypt=True;TrustServerCertificate=True",
            [$"{DatabaseOptions.SectionName}:CommandTimeout"] = "00:00:12",
        });

        var options = provider.GetRequiredService<IOptionsMonitor<SqlServerCacheOptions>>().Get(CachingRegistration.SqlServerCacheKey);

        var connection = new SqlConnectionStringBuilder(options.ConnectionString);
        Assert.Equal("localhost", connection.DataSource);
        Assert.Equal("ErpAiPro", connection.InitialCatalog);
        Assert.True(connection.IntegratedSecurity);
        Assert.Equal(12, connection.CommandTimeout);
        Assert.Equal(CachingDbContext.SchemaName, options.SchemaName);
        Assert.Equal(CachingDbContext.DistributedCacheTable, options.TableName);
        Assert.Equal(TimeSpan.FromMinutes(5), options.ExpiredItemsDeletionInterval);
        Assert.IsType<SystemClock>(options.SystemClock);
    }

    [Fact]
    public void AddErpCaching_UnnamedSqlServerCacheOptions_AreLeftAsTheyAre()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Server=localhost;Database=ErpAiPro;Integrated Security=True",
        });

        var options = provider.GetRequiredService<IOptions<SqlServerCacheOptions>>().Value;

        Assert.Null(options.ConnectionString);
        Assert.Null(options.SchemaName);
        Assert.Null(options.TableName);
    }

    [Fact]
    public void AddErpCaching_WithoutAConnectionString_LeavesTheCacheConnectionUnset()
    {
        using var provider = Build([]);

        var options = provider.GetRequiredService<IOptionsMonitor<SqlServerCacheOptions>>().Get(CachingRegistration.SqlServerCacheKey);

        Assert.Null(options.ConnectionString);
    }

    [Fact]
    public void AddErpCaching_SqlServerCache_IsRegisteredOnlyUnderItsKeyBehindTheRetriesSoHybridCacheStaysInProcess()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Server=localhost;Database=ErpAiPro;Integrated Security=True",
        });

        Assert.IsType<RetryingSqlServerCache>(provider.GetRequiredKeyedService<IDistributedCache>(CachingRegistration.SqlServerCacheKey));
        Assert.Null(provider.GetService<IDistributedCache>());
    }

    [Fact]
    public void CreateRetryProvider_DatabaseRetrySettings_TriesOnceMoreThanTheRetryCountWithExponentialDelaysUpToTheMaximum()
    {
        var provider = RetryingSqlServerCache.CreateRetryProvider(new DatabaseOptions { MaxRetryCount = 3, MaxRetryDelay = TimeSpan.FromSeconds(7) });

        Assert.Equal(4, provider.RetryLogic.NumberOfTries);
        Assert.Equal(TimeSpan.FromSeconds(1), provider.RetryLogic.RetryIntervalEnumerator.GapTimeInterval);
        Assert.Equal(TimeSpan.FromSeconds(7), provider.RetryLogic.RetryIntervalEnumerator.MaxTimeInterval);
        Assert.Equal(TimeSpan.Zero, provider.RetryLogic.RetryIntervalEnumerator.MinTimeInterval);
        Assert.False(provider.RetryLogic.TransientPredicate(new TimeoutException("Not a SQL Server error.")));
    }

    [Theory]
    [InlineData(0, "00:00:01")]
    [InlineData(20, "00:02:00")]
    public void CreateRetryProvider_EveryValidDatabaseSetting_BuildsAProvider(int maxRetryCount, string maxRetryDelay)
    {
        var provider = RetryingSqlServerCache.CreateRetryProvider(new DatabaseOptions { MaxRetryCount = maxRetryCount, MaxRetryDelay = TimeSpan.Parse(maxRetryDelay, CultureInfo.InvariantCulture) });

        Assert.Equal(maxRetryCount + 1, provider.RetryLogic.NumberOfTries);
    }

    [Fact]
    public void AddErpCaching_CacheContext_JoinsTheCatalogueUnderItsSchema()
    {
        using var provider = Build([]);

        var registration = Assert.Single(provider.GetServices<DbContextRegistration>());

        Assert.Equal(typeof(CachingDbContext), registration.ContextType);
        Assert.Equal(CachingDbContext.SchemaName, registration.Schema);
    }

    [Theory]
    [InlineData(nameof(CachingOptions.DefaultExpiration), "00:00:00.999")]
    [InlineData(nameof(CachingOptions.DefaultExpiration), "1.00:00:01")]
    [InlineData(nameof(CachingOptions.MaximumPayloadBytes), "1023")]
    [InlineData(nameof(CachingOptions.MaximumPayloadBytes), "67108865")]
    public void Validate_ValueOutsideTheBound_Fails(string property, string value)
    {
        var options = new CachingOptions();
        var info = typeof(CachingOptions).GetProperty(property)!;
        info.SetValue(options, info.PropertyType == typeof(TimeSpan) ? TimeSpan.Parse(value, CultureInfo.InvariantCulture) : long.Parse(value, CultureInfo.InvariantCulture));

        var failures = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), failures, validateAllProperties: true));
        Assert.Contains(property, Assert.Single(failures).MemberNames);
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
        builder.AddErpCaching();

        return builder.Services.BuildServiceProvider();
    }
}
