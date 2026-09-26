using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        builder.AddErpCaching();

        return builder.Services.BuildServiceProvider();
    }
}
