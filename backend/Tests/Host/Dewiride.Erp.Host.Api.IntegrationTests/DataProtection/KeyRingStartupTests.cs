using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
using Azure;
using Azure.Core.Cryptography;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Deployment;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.DataProtection;

public sealed class KeyRingStartupTests
{
    private static readonly TimeSpan LiveDataHorizon = TimeSpan.FromHours(12) + TimeSpan.FromMinutes(5);

    [Fact]
    public async Task Start_WhenKeyVaultRefusesToUnwrapWithTheKey_FailsLoggingTheStatusAndErrorCode()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        var logs = new FakeLogCollector();
        await using var root = new ErpApiFactory().WithDeployment(deployment);
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(new FakeLoggerProvider(logs));
            services.AddSingleton<IKeyEncryptionKeyResolver>(new RefusingKeyEncryptionKeyResolver(deployment.KeyResolver!));
        }));

        var failure = Assert.Throws<RequestFailedException>(() => factory.Services);

        Assert.Equal(RefusingKeyEncryptionKeyResolver.Status, failure.Status);
        Assert.Equal(2, deployment.KeyResolver!.Resolutions);
        var record = Assert.Single(logs.GetSnapshot(), entry => entry.Category == typeof(KeyRingStartupCheck).FullName);
        Assert.Equal(LogLevel.Critical, record.Level);
        Assert.Equal(RefusingKeyEncryptionKeyResolver.Status.ToString(CultureInfo.InvariantCulture), record.GetStructuredStateValue("Status"));
        Assert.Equal(RefusingKeyEncryptionKeyResolver.ErrorCode, record.GetStructuredStateValue("ErrorCode"));
        Assert.Empty(deployment.KeyFiles());
    }

    [Fact]
    public async Task Start_WhenTheKeyResolvesWithoutAVersion_FailsBeforeAnyKeyRecordsTheVersionlessIdentifier()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        await using var root = new ErpApiFactory().WithDeployment(deployment);
        await using var factory = WithResolver(root, new TestKeyEncryptionKeyResolver(deployment.KeyIdentifier!.AbsoluteUri));

        var failure = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("keys/read", failure.Message, StringComparison.Ordinal);
        Assert.Empty(deployment.KeyFiles());
    }

    [Fact]
    public async Task Start_WhenTheKeyDirectoryCannotBeWritten_FailsInTheFolderProbeBeforeAnyOtherCheck()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        var blocked = new DirectoryInfo(Path.Combine(deployment.KeyDirectory!.FullName, "blocked"));
        await File.WriteAllTextAsync(blocked.FullName, "a file where the key directory should be", TestContext.Current.CancellationToken);
        await using var root = new ErpApiFactory().WithDeployment(deployment);
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<KeyManagementOptions>(options => options.XmlRepository = new FileSystemXmlRepository(blocked, NullLoggerFactory.Instance))));

        var failure = Assert.Throws<IOException>(() => factory.Services);

        Assert.Equal(KeyDirectoryProbe.UnwritableDirectoryMessage(blocked.FullName), failure.Message);
        Assert.Equal(0, deployment.KeyResolver!.Resolutions);
    }

    [Fact]
    public async Task Start_WhenAnExistingKeyCannotBeDecrypted_FailsInsteadOfCreatingANewKey()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        await using (var first = new ErpApiFactory().WithDeployment(deployment))
        {
            _ = first.Services;
        }

        await using var root = new ErpApiFactory().WithDeployment(deployment);
        await using var factory = WithResolver(root, TestKeyEncryptionKeyResolver.ForVersionOf(deployment.KeyIdentifier!));

        Assert.ThrowsAny<CryptographicException>(() => factory.Services);
        Assert.Single(deployment.KeyFiles());
    }

    [Fact]
    public async Task Start_WhenAKeyThatCannotBeDecryptedIsRevoked_StartsWithANewKey()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        var now = TimeProvider.System.GetUtcNow();
        var revoked = PlantedKeys.WrappedByAnotherKey(deployment, now.AddDays(-1), now.AddDays(89), revoked: true);
        await using var factory = new ErpApiFactory().WithDeployment(deployment);

        var keys = factory.Services.GetRequiredService<IKeyManager>().GetAllKeys();

        Assert.Equal(2, keys.Count);
        Assert.True(Assert.Single(keys, key => key.KeyId == revoked.KeyId).IsRevoked);
    }

    [Fact]
    public async Task Start_WhenAKeyThatCannotBeDecryptedExpiredBeforeTheOldestLivingSessionBegan_StartsWithANewKey()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        var expiration = clock.GetUtcNow() - LiveDataHorizon - TimeSpan.FromSeconds(1);
        var expired = PlantedKeys.WrappedByAnotherKey(deployment, expiration.AddDays(-90), expiration);
        await using var factory = OnTheClock(new ErpApiFactory().WithDeployment(deployment), clock);

        var keys = factory.Services.GetRequiredService<IKeyManager>().GetAllKeys();

        Assert.Equal(2, keys.Count);
        Assert.Contains(keys, key => key.KeyId == expired.KeyId);
    }

    [Fact]
    public async Task Start_WhenAKeyThatCannotBeDecryptedExpiredAfterTheOldestLivingSessionBegan_Fails()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        var expiration = clock.GetUtcNow() - LiveDataHorizon + TimeSpan.FromSeconds(1);
        PlantedKeys.WrappedByAnotherKey(deployment, expiration.AddDays(-90), expiration);
        await using var factory = OnTheClock(new ErpApiFactory().WithDeployment(deployment), clock);

        Assert.ThrowsAny<CryptographicException>(() => factory.Services);
        Assert.Single(deployment.KeyFiles());
    }

    [Fact]
    public async Task Start_WhenTheKeyFolderHoldsAKeyThatKeyVaultDoesNotWrap_FailsNamingOnlyItsFile()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        var now = TimeProvider.System.GetUtcNow();
        var planted = PlantedKeys.Unwrapped(deployment, now, now.AddDays(90));
        var file = Assert.Single(deployment.KeyFiles());
        var masterKey = Assert.Single(XDocument.Load(file.FullName).Descendants(), element => element.Name.LocalName == "masterKey").Element("value")!.Value;
        var logs = new FakeLogCollector();
        await using var root = new ErpApiFactory().WithDeployment(deployment);
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(new FakeLoggerProvider(logs))));

        var failure = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Equal(KeyRingStartupCheck.UnwrappedKeyMessage($"key-{planted.KeyId:D}.xml"), failure.Message);
        Assert.Single(deployment.KeyFiles());
        var record = Assert.Single(logs.GetSnapshot(), entry => entry.Category == typeof(KeyRingStartupCheck).FullName);
        Assert.Equal(LogLevel.Critical, record.Level);
        Assert.DoesNotContain(logs.GetSnapshot(), entry =>
            entry.Message.Contains(masterKey, StringComparison.Ordinal)
            || (entry.Exception?.ToString().Contains(masterKey, StringComparison.Ordinal) ?? false)
            || (entry.StructuredState?.Any(pair => pair.Value?.Contains(masterKey, StringComparison.Ordinal) ?? false) ?? false));
    }

    [Fact]
    public async Task Start_WhenAWrappedKeyAlsoCarriesAMasterKeyInTheClear_FailsInsteadOfUsingIt()
    {
        using var deployment = TestDeployment.WithPersistedKeyRing();
        await using (var first = new ErpApiFactory().WithDeployment(deployment))
        {
            _ = first.Services;
        }

        var file = Assert.Single(deployment.KeyFiles());
        var key = XDocument.Load(file.FullName);
        var descriptor = key.Root!.Element("descriptor")!.Elements().Single();
        descriptor.AddFirst(new XElement("masterKey", new XElement("value", Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)))));
        key.Save(file.FullName);
        await using var factory = new ErpApiFactory().WithDeployment(deployment);

        var failure = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Equal(KeyRingStartupCheck.UnwrappedKeyMessage(file.Name), failure.Message);
    }

    private static WebApplicationFactory<Program> WithResolver(ErpApiFactory root, IKeyEncryptionKeyResolver resolver) =>
        root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton(resolver)));

    private static WebApplicationFactory<Program> OnTheClock(ErpApiFactory root, FakeTimeProvider clock) =>
        root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
}
