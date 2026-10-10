using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Logging;

// Microsoft.Identity.Web installs its own adapter in IdentityModel's process-wide logger when it first builds the OpenID
// Connect options, and only while that logger is still IdentityModel's null logger. This runs as the host starts, before the
// first request builds them, and replaces whatever is installed, so the bounded logger is the one in place.
internal sealed class IdentityModelLoggerInstaller(BoundedIdentityModelLogger logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogHelper.Logger = logger;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
