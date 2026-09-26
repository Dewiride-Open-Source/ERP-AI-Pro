using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.Host.Composition;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Blob;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Configuration;

public sealed class AttachmentSettingsStartupTests
{
    [Fact]
    public async Task StartAsync_BlobServiceUriAndEmulatorHostBothSet_FailsNamingBothSettings()
    {
        using var host = BuildHost(
            (ErpApiFactory.AttachmentsBlobServiceUriKey, "https://erpaipro.blob.core.windows.net/"),
            (ErpApiFactory.AttachmentsEmulatorHostKey, BlobTestContainer.Current.EmulatorHost),
            (ErpApiFactory.AttachmentsContainerNameKey, BlobTestContainer.Current.ContainerName),
            (ErpApiFactory.AttachmentsEncryptionKeyKey, $"startup:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}"));

        var failure = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(typeof(AttachmentsOptions), failure.OptionsType);
        Assert.Contains(ErpApiFactory.AttachmentsBlobServiceUriKey, failure.Message, StringComparison.Ordinal);
        Assert.Contains(ErpApiFactory.AttachmentsEmulatorHostKey, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WithoutEncryptionKey_FailsNamingTheSetting()
    {
        using var host = BuildHost(
            (ErpApiFactory.AttachmentsEmulatorHostKey, BlobTestContainer.Current.EmulatorHost),
            (ErpApiFactory.AttachmentsContainerNameKey, BlobTestContainer.Current.ContainerName));

        var failure = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(typeof(AttachmentsOptions), failure.OptionsType);
        Assert.Contains($"{ErpApiFactory.AttachmentsEncryptionKeyKey} is required", failure.Message, StringComparison.Ordinal);
    }

    private static IHost BuildHost(params (string Key, string Value)[] attachmentSettings)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = Environments.Production,
        });
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.InMemorySource,
            [ErpApiFactory.DatabaseConnectionKey] = SqlTestDatabase.Current.ConnectionString,
        };
        foreach (var (key, value) in attachmentSettings)
        {
            settings[key] = value;
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddErpPlatform(typeof(Program).Assembly);

        return builder.Build();
    }
}
