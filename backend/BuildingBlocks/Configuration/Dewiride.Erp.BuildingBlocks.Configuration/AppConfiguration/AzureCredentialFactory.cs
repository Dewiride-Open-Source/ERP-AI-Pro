using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Configuration.AppConfiguration;

internal static class AzureCredentialFactory
{
    public const string SelectionVariable = "AZURE_TOKEN_CREDENTIALS";

    public const string RuntimeSelection = "EnvironmentCredential";

    public const string DevelopmentSelection = "AzureCliCredential";

    private const string TenantIdVariable = "AZURE_TENANT_ID";

    private const string ClientIdVariable = "AZURE_CLIENT_ID";

    private const string CertificatePathVariable = "AZURE_CLIENT_CERTIFICATE_PATH";

    private const string ClientSecretVariable = "AZURE_CLIENT_SECRET";

    private static readonly string[] CertificateExtensions = [".pem", ".pfx"];

    public static TokenCredential Create(IHostEnvironment environment, Func<string, string?> environmentVariable)
    {
        var selection = environmentVariable(SelectionVariable);
        if (string.IsNullOrWhiteSpace(selection))
        {
            throw new InvalidOperationException(
                $"{SelectionVariable} must be set: '{DevelopmentSelection}' on a developer machine, '{RuntimeSelection}' in the api container.");
        }

        if (!environment.IsDevelopment())
        {
            ValidateRuntimeIdentity(environment.EnvironmentName, selection, environmentVariable);
        }

        return new DefaultAzureCredential();
    }

    private static void ValidateRuntimeIdentity(string environmentName, string selection, Func<string, string?> environmentVariable)
    {
        if (!string.Equals(selection, RuntimeSelection, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{SelectionVariable} must be '{RuntimeSelection}' when ASPNETCORE_ENVIRONMENT is '{environmentName}'; value '{selection}' was rejected.");
        }

        if (string.IsNullOrWhiteSpace(environmentVariable(TenantIdVariable)))
        {
            throw new InvalidOperationException(
                $"{TenantIdVariable} must be set to the Entra tenant id of the runtime service principal.");
        }

        if (string.IsNullOrWhiteSpace(environmentVariable(ClientIdVariable)))
        {
            throw new InvalidOperationException(
                $"{ClientIdVariable} must be set to the application (client) id of the runtime service principal.");
        }

        var certificatePath = environmentVariable(CertificatePathVariable);
        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            throw new InvalidOperationException(
                $"{CertificatePathVariable} must be set to the runtime client certificate file mounted as a compose secret (/run/secrets/erp-runtime-client.pem).");
        }

        if (!CertificateExtensions.Contains(Path.GetExtension(certificatePath), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{CertificatePathVariable} must point to a .pem or .pfx file; '{certificatePath}' was rejected.");
        }

        RequireReadable(certificatePath);

        if (!string.IsNullOrEmpty(environmentVariable(ClientSecretVariable)))
        {
            throw new InvalidOperationException(
                $"{ClientSecretVariable} must not be set: the runtime identity authenticates with a certificate only.");
        }
    }

    private static void RequireReadable(string certificatePath)
    {
        try
        {
            using var handle = File.OpenHandle(certificatePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"{CertificatePathVariable} points to '{certificatePath}', which cannot be opened for reading by the process user. " +
                "Mount the runtime client certificate at that path as a regular file owned by the user the api container runs as, with mode 0400.",
                exception);
        }
    }
}
