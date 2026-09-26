using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.Modules.Platform.Attachments.Files.Endpoints.Uploads;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Modules.Platform.Attachments.UnitTests.Files.Endpoints.Uploads;

public sealed class UploadSizeLimitTests
{
    private const string MaxSizeBytesKey = $"{AttachmentsOptions.SectionName}:{nameof(AttachmentsOptions.MaxSizeBytes)}";

    [Fact]
    public void MaxRequestBodySize_ConfiguredLimit_AddsSixtyFourKibibytesOfMultipartFraming()
    {
        var configuration = Configuration(1024);
        using var services = Services(configuration);

        var limit = new UploadSizeLimit(services.GetRequiredService<IOptionsMonitor<AttachmentsOptions>>());

        Assert.Equal(1024 + (64 * 1024), limit.MaxRequestBodySize);
    }

    [Fact]
    public void MaxRequestBodySize_AfterTheConfigurationReloads_FollowsTheNewLimit()
    {
        var configuration = Configuration(1024);
        using var services = Services(configuration);
        var limit = new UploadSizeLimit(services.GetRequiredService<IOptionsMonitor<AttachmentsOptions>>());
        Assert.Equal(1024 + UploadSizeLimit.MultipartAllowanceBytes, limit.MaxRequestBodySize);

        configuration[MaxSizeBytesKey] = (5L * 1024 * 1024).ToString(CultureInfo.InvariantCulture);
        configuration.Reload();

        Assert.Equal((5L * 1024 * 1024) + UploadSizeLimit.MultipartAllowanceBytes, limit.MaxRequestBodySize);
    }

    private static IConfigurationRoot Configuration(long maxSizeBytes) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [MaxSizeBytesKey] = maxSizeBytes.ToString(CultureInfo.InvariantCulture) })
            .Build();

    private static ServiceProvider Services(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddOptions<AttachmentsOptions>().Bind(configuration.GetSection(AttachmentsOptions.SectionName));

        return services.BuildServiceProvider();
    }
}
