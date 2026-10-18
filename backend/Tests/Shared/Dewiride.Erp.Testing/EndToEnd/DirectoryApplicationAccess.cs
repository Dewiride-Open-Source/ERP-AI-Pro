using Dewiride.Erp.Testing.Graph;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.EndToEnd;

// The account check reads the directory with the application's own token, and the test directory refuses that token for
// every request of the host while this switch is on, so a spec turns it on to show how the ERP answers a directory without
// the application permission, and off again, only inside its turn of the account check. Like the other test routes, it is
// the test host's own: anonymous, taking no antiforgery token, and mapped only in the Testing environment.
public static class DirectoryApplicationAccess
{
    public const string Path = "/api/__test/directory/application-access";

    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var environment = routes.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsEnvironment(EndToEndHost.EnvironmentName))
        {
            throw new InvalidOperationException(
                $"Anyone can refuse the test directory's application token, so the route is mapped only in the {EndToEndHost.EnvironmentName} environment; this host runs in {environment.EnvironmentName}.");
        }

        routes.MapPut(Path, Set).AllowAnonymous().DisableAntiforgery();
    }

    private static Results<NoContent, ValidationProblem> Set(DirectoryApplicationAccessSeed access, TestDirectory directory)
    {
        if (access.Refused is not { } refused)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["refused"] = ["Say whether the directory refuses the application's token."],
            });
        }

        if (refused)
        {
            directory.RefuseApplicationTokens();
        }
        else
        {
            directory.AllowApplicationTokens();
        }

        return TypedResults.NoContent();
    }
}
