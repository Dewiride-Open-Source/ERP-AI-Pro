using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.Attachments.Inspection;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Options;

internal sealed partial class AttachmentsOptionsValidator : IValidateOptions<AttachmentsOptions>
{
    private const string Section = AttachmentsOptions.SectionName;

    private static readonly string[] LocalEmulatorHosts = ["127.0.0.1", "localhost", "host.docker.internal"];

    public ValidateOptionsResult Validate(string? name, AttachmentsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        ValidateStorageTarget(options, failures);

        if (!ContainerNamePattern().IsMatch(options.ContainerName ?? string.Empty))
        {
            failures.Add($"{Section}:ContainerName must be 3 to 63 lowercase letters, digits and single hyphens, starting and ending with a letter or digit.");
        }

        if (!ContentTypes.TryParseAllowList(options.AllowedContentTypes, out _, out var typesProblem))
        {
            failures.Add(typesProblem);
        }

        if (options.UploadReservationLifetime < 2 * options.TransferTimeout)
        {
            failures.Add($"{Section}:UploadReservationLifetime must be at least twice {Section}:TransferTimeout, so an upload still in progress is never swept.");
        }

        if (!EncryptionKeys.TryParse(options.EncryptionKey, options.RetiredEncryptionKeys, out _, out var keyProblem))
        {
            failures.Add(keyProblem);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateStorageTarget(AttachmentsOptions options, List<string> failures)
    {
        var hasService = !string.IsNullOrWhiteSpace(options.BlobServiceUri);
        var hasEmulator = !string.IsNullOrWhiteSpace(options.EmulatorHost);
        if (hasService == hasEmulator)
        {
            failures.Add(
                $"Set exactly one of {Section}:BlobServiceUri (the https blob endpoint of the storage account, written to App Configuration by " +
                $"scripts/azure/provision.sh) and {Section}:EmulatorHost (the host running the Azurite emulator, for tests and the local containers).");
            return;
        }

        if (hasService)
        {
            if (!Uri.TryCreate(options.BlobServiceUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.UserInfo.Length > 0)
            {
                failures.Add($"{Section}:BlobServiceUri must be the https blob endpoint of the storage account, such as https://<account>.blob.core.windows.net/, without a path or query.");
            }

            return;
        }

        var host = options.EmulatorHost!.Trim();
        if (!LocalEmulatorHosts.Contains(host, StringComparer.OrdinalIgnoreCase) && !ServiceNamePattern().IsMatch(host))
        {
            failures.Add($"{Section}:EmulatorHost must be 127.0.0.1, localhost, host.docker.internal or the single-label name of a compose service; '{host}' was rejected.");
        }
    }

    [GeneratedRegex("^(?!.*--)[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex ContainerNamePattern();

    [GeneratedRegex("^[a-z][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex ServiceNamePattern();
}
