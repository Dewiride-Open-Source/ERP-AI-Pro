using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

// The sign-in registrations get their app roles from this file through graph.sh; a role the API checks that the registration
// does not define is one nobody can be given, and a role an application could hold would reach the routes it guards.
public sealed class AppRolesTests
{
    [Fact]
    public void RegisteredAppRoles_AreTheRolesTheApiChecksAndArePeoplesOnly()
    {
        using var roles = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Combine("scripts", "azure", "entra", "app-roles.json")));

        var registered = roles.RootElement.EnumerateArray()
            .Where(role => role.GetProperty("isEnabled").GetBoolean())
            .ToList();

        Assert.Equal([AppRoles.Administrator, AppRoles.User], registered.Select(role => role.GetProperty("value").GetString()).Order(StringComparer.Ordinal));
        Assert.All(registered, role => Assert.Equal(["User"], role.GetProperty("allowedMemberTypes").EnumerateArray().Select(type => type.GetString())));
    }
}
