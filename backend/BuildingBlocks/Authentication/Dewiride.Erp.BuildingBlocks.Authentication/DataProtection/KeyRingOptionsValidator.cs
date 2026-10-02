using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

// Without the setting the key ring lives in memory, which only a run that cannot reach the store may use: every restart
// would sign everyone out. The value is never echoed, because a mistyped one could carry credentials.
internal sealed partial class KeyRingOptionsValidator(ErpConfigurationInfo configuration, IHostEnvironment environment) : IValidateOptions<KeyRingOptions>
{
    private const string Setting = KeyRingOptions.KeyIdentifierKey;

    private const string Shape = "https://<vault>.vault.azure.net/keys/<name>";

    public ValidateOptionsResult Validate(string? name, KeyRingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var keyIdentifier = options.KeyIdentifier;
        if (keyIdentifier is null || string.IsNullOrWhiteSpace(keyIdentifier.OriginalString))
        {
            return configuration.Source == ErpConfigurationSource.AppConfiguration || environment.IsProduction()
                ? ValidateOptionsResult.Fail(
                    $"{Setting} is required when the API reads its settings from App Configuration or runs in Production: the versionless identifier " +
                    $"of the Key Vault key that protects the Data Protection key ring, {Shape}.")
                : ValidateOptionsResult.Success;
        }

        if (!keyIdentifier.IsAbsoluteUri
            || keyIdentifier.Scheme != Uri.UriSchemeHttps
            || !keyIdentifier.IsDefaultPort
            || keyIdentifier.UserInfo.Length > 0
            || keyIdentifier.Query.Length > 0
            || keyIdentifier.Fragment.Length > 0
            || !VaultHost().IsMatch(keyIdentifier.Host))
        {
            return ValidateOptionsResult.Fail(
                $"{Setting} must be the identifier of a Key Vault key, {Shape}, an https address without port, credentials, query or fragment " +
                "on a vault name of 3 to 24 letters, digits or hyphens.");
        }

        if (VersionedKeyPath().IsMatch(keyIdentifier.AbsolutePath))
        {
            return ValidateOptionsResult.Fail(
                $"{Setting} must be versionless, {Shape} without a version, so that a rotated key keeps working: each Data Protection key " +
                "records the version that wrapped it, and new keys are wrapped with the current version.");
        }

        return KeyPath().IsMatch(keyIdentifier.AbsolutePath)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{Setting} must name a key with the path /keys/<name>, {Shape}, where the name is 1 to 127 letters, digits or hyphens.");
    }

    [GeneratedRegex(@"^[A-Za-z0-9-]{3,24}\.vault\.azure\.net$", RegexOptions.CultureInvariant)]
    private static partial Regex VaultHost();

    [GeneratedRegex("^/keys/[A-Za-z0-9-]{1,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPath();

    [GeneratedRegex("^/keys/[A-Za-z0-9-]{1,127}/[^/]+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionedKeyPath();
}
