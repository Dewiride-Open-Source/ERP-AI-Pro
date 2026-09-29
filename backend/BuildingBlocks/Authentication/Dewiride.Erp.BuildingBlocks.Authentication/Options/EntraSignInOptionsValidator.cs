using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Authentication.Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Options;

internal sealed partial class EntraSignInOptionsValidator(TimeProvider timeProvider, IHostEnvironment environment) : IValidateOptions<EntraSignInOptions>
{
    private const string Section = EntraSignInOptions.SectionName;

    public ValidateOptionsResult Validate(string? name, EntraSignInOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        ValidateDirectoryId(options.TenantId, "TenantId", "the directory (tenant) id of the Entra tenant; the multi-tenant values common, organizations and consumers are refused", failures);
        ValidateDirectoryId(options.ClientId, "ClientId", "the application (client) id of the sign-in app registration", failures);

        if (!Uri.TryCreate(options.Instance, UriKind.Absolute, out var instance) || instance.Scheme != Uri.UriSchemeHttps || instance.AbsolutePath != "/" || instance.Query.Length > 0 || instance.Fragment.Length > 0 || instance.UserInfo.Length > 0)
        {
            failures.Add($"{Section}:Instance must be the https address of the Entra cloud without a path, such as {EntraSignInOptions.DefaultInstance}.");
        }

        ValidateWebOrigin(options.WebOrigin, failures);
        ValidateCertificate(options.ClientCertificate, failures);

        if (options.SessionLifetime < options.SessionIdleTimeout)
        {
            failures.Add($"{Section}:SessionLifetime must be at least {Section}:SessionIdleTimeout.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateDirectoryId(string? value, string key, string meaning, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{Section}:{key} is required: {meaning}.");
        }
        else if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            failures.Add($"{Section}:{key} must be a GUID: {meaning}.");
        }
    }

    // The same rule as require_origin in scripts/azure/lib/graph.sh, which registers the redirect URIs built from this origin.
    private void ValidateWebOrigin(string? origin, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            failures.Add($"{Section}:WebOrigin is required: the origin people open the web app at, such as https://erp.example.com.");
            return;
        }

        var local = !environment.IsProduction() && LocalHttpOrigin().IsMatch(origin);
        if (!local && !HttpsOrigin().IsMatch(origin))
        {
            failures.Add(
                $"{Section}:WebOrigin must be an https origin made of a host name and an optional port, without path, wildcard, credentials, query or trailing slash " +
                $"(http is accepted for localhost and 127.0.0.1 outside Production); '{origin}' was rejected.");
        }
    }

    private void ValidateCertificate(string? certificate, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(certificate))
        {
            failures.Add($"{Section}:ClientCertificate is required: the Key Vault reference to the sign-in certificate Erp--Platform--Identity--ClientCertificate, a base64 PKCS#12.");
            return;
        }

        if (SignInCertificate.FindProblem(certificate, timeProvider.GetUtcNow()) is { } problem)
        {
            failures.Add($"{Section}:ClientCertificate {problem}");
        }
    }

    [GeneratedRegex(@"^https://[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*(:[0-9]{1,5})?$", RegexOptions.CultureInvariant)]
    private static partial Regex HttpsOrigin();

    [GeneratedRegex(@"^http://(localhost|127\.0\.0\.1)(:[0-9]{1,5})?$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalHttpOrigin();
}
