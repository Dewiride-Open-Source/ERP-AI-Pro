using Azure;
using Azure.Security.KeyVault.Secrets;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.AppConfiguration.Fakes;

internal sealed class FakeSecretClient(Uri vaultUri) : SecretClient
{
    public List<string> Requested { get; } = [];

    public override Uri VaultUri => vaultUri;

    public static string ValueOf(string name) => $"value of {name}";

    public override Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
    {
        Requested.Add(name);

        return Task.FromResult(Response.FromValue(new KeyVaultSecret(name, ValueOf(name)), new FakeResponse()));
    }
}
