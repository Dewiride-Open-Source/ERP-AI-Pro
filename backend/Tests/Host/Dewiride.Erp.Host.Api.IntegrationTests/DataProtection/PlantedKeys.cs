using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;
using Dewiride.Erp.Testing.Deployment;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace Dewiride.Erp.Host.Api.IntegrationTests.DataProtection;

// Writes a key into a deployment's key folder as another installation of Data Protection would: wrapped by a Key Vault key of
// its own, which the deployment's key cannot unwrap, or not wrapped at all.
internal static class PlantedKeys
{
    public static IKey WrappedByAnotherKey(TestDeployment deployment, DateTimeOffset activation, DateTimeOffset expiration, bool revoked = false)
    {
        var anotherKey = TestKeyEncryptionKeyResolver.ForVersionOf(deployment.KeyIdentifier!);

        return Plant(deployment, builder => builder.ProtectKeysWithAzureKeyVault(deployment.KeyIdentifier!, anotherKey), activation, expiration, revoked);
    }

    public static IKey Unwrapped(TestDeployment deployment, DateTimeOffset activation, DateTimeOffset expiration) =>
        Plant(deployment, static _ => { }, activation, expiration, revoked: false);

    private static IKey Plant(TestDeployment deployment, Action<IDataProtectionBuilder> protect, DateTimeOffset activation, DateTimeOffset expiration, bool revoked)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddDataProtection()
            .SetApplicationName(DataProtectionKeyRing.ApplicationName)
            .PersistKeysToFileSystem(deployment.KeyDirectory!);
        protect(builder);
        using var provider = services.BuildServiceProvider();
        var keyManager = provider.GetRequiredService<IKeyManager>();

        var key = keyManager.CreateNewKey(activation, expiration);
        if (revoked)
        {
            keyManager.RevokeKey(key.KeyId, "Revoked by the test that planted it.");
        }

        return key;
    }
}
