using System.Net;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Configuration.Hosting;

internal sealed class ErpHostOptionsValidator : IValidateOptions<ErpHostOptions>
{
    public ValidateOptionsResult Validate(string? name, ErpHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        for (var index = 0; index < options.KnownNetworks.Count; index++)
        {
            var network = options.KnownNetworks[index];
            if (!IsExactNetwork(network))
            {
                failures.Add($"{ErpHostOptions.SectionName}:KnownNetworks:{index} is '{network}', which is not a network in CIDR notation with its network address, such as 172.28.0.0/16.");
            }
        }

        if (options.AllowedHosts.Split(';').Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{ErpHostOptions.SectionName}:AllowedHosts '{options.AllowedHosts}' contains an empty entry; separate host names with a single ';'.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    // .NET 10 clears host bits instead of rejecting them, so 172.28.0.5/16 would silently trust the whole /16.
    private static bool IsExactNetwork(string value)
    {
        var slash = value.IndexOf('/', StringComparison.Ordinal);

        return slash > 0
            && IPAddress.TryParse(value.AsSpan(0, slash), out var address)
            && IPNetwork.TryParse(value, out var network)
            && network.BaseAddress.Equals(address);
    }
}
