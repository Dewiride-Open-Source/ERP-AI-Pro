using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Dewiride.Erp.Testing.EndToEnd;

// The host signs anyone in as a test persona, so it starts only when the environment says Testing, which no deployment of
// the API is given; the API image never contains this assembly in the first place.
public sealed record EndToEndHostSettings(int Port, string WebOrigin)
{
    public const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";

    public const string PortArgument = "port";

    public const string WebOriginArgument = "web-origin";

    public static EndToEndHostSettings Read(string[] args, Func<string, string?> environmentVariable)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environmentVariable);

        var environment = environmentVariable(EnvironmentVariable);
        if (!string.Equals(environment, EndToEndHost.EnvironmentName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The end-to-end host signs anyone in as a test persona, so it starts only when {EnvironmentVariable} is {EndToEndHost.EnvironmentName}; '{environment}' was refused.");
        }

        var arguments = new ConfigurationBuilder().AddCommandLine(args).Build();
        var port = arguments[PortArgument];
        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > ushort.MaxValue)
        {
            throw new InvalidOperationException($"--{PortArgument} must be the port the API listens on, from 1 to {ushort.MaxValue}; '{port}' was refused.");
        }

        var webOrigin = arguments[WebOriginArgument];
        if (string.IsNullOrWhiteSpace(webOrigin))
        {
            throw new InvalidOperationException($"--{WebOriginArgument} must be the origin Playwright opens the web app at, such as https://localhost:3200.");
        }

        return new EndToEndHostSettings(number, webOrigin);
    }
}
