using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.EndToEnd;

// Playwright signs a test in through the web origin, where a browser completes a real sign-in, so the session cookie and
// the antiforgery pair land on the origin the pages come from; the route is under /api, a prefix the web app hands to the
// API. Every sign-in is a new person of the persona, admitted to the test token endpoint first: a test that signs out ends
// the sessions of its own account only, and tests running side by side never share a rate limit. The answer names the
// person's Entra session and its issuer, which a spec sends to the front-channel sign-out the way Entra would.
public static class PersonaSignIn
{
    public const string PathPrefix = "/api/__test/sign-in";

    private static readonly Dictionary<string, TestUser> Personas = new(StringComparer.Ordinal)
    {
        ["accountant"] = TestUsers.Accountant,
        ["administrator"] = TestUsers.Administrator,
    };

    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var environment = routes.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsEnvironment(EndToEndHost.EnvironmentName))
        {
            throw new InvalidOperationException(
                $"The persona sign-in signs anyone in, so it is mapped only in the {EndToEndHost.EnvironmentName} environment; this host runs in {environment.EnvironmentName}.");
        }

        routes.MapPost($"{PathPrefix}/{{persona}}", SignInAsync).AllowAnonymous().DisableAntiforgery();
    }

    private static async Task<Results<Ok<PersonaSignInResponse>, NotFound>> SignInAsync(string persona, HttpContext context, TestTokenEndpoint tokenEndpoint)
    {
        if (!Personas.TryGetValue(persona, out var template))
        {
            return TypedResults.NotFound();
        }

        var person = template with { ObjectId = Guid.CreateVersion7() };
        tokenEndpoint.Admit(person);
        await TestSignIn.IssueSessionAsync(context, person).ConfigureAwait(false);

        return TypedResults.Ok(new PersonaSignInResponse(person.ObjectId, person.Name, person.UserName, person.Roles, person.EntraSessionId, TestIdentityProvider.Issuer));
    }
}
