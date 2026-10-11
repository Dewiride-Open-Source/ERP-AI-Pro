using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Authentication.Permissions;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Host.Api.IntegrationTests.BearerTokens;
using Dewiride.Erp.Modules.Platform.Attachments.Contracts.Files;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.Authentication.BearerTokens;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Permissions;

internal static class PermissionRoutes
{
    public const string RolesPath = "/__test/roles";

    public const string BearerRolesPath = "/__test/bearer/roles";

    public const string BearerFilesPath = "/__test/bearer/files";

    public static void Map(IEndpointRouteBuilder routes)
    {
        TestSignIn.Map(routes);
        routes.MapGet(RolesPath, RolesOf);
        routes.MapGet(BearerRolesPath, RolesOf).RequireBearerToken(BearerTokenRoutes.Access);
        routes.MapGet(BearerFilesPath, () => Results.NoContent()).RequireBearerToken(BearerTokenRoutes.Access).RequirePermission(AttachmentsPermissions.Read);
    }

    public static ErpApiFactory Factory() => new ErpApiFactory().WithFeature(BearerTokenFeature.Name, true).WithTestEndpoints(Map);

    // A person's token is accepted only while the person's record admits them, and their first sign-in is what creates it.
    public static async Task<TestUser> AdmittedPersonAsync(ErpApiFactory factory, params string[] appRoles)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var objectId = Guid.CreateVersion7();
        var person = new TestUser(objectId, "Ravi Kumar", $"ravi.kumar.{objectId:N}@example.com", appRoles);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IPersonAdmission>().AdmitAsync(new SignedInPerson(person.ObjectId, person.Name, person.UserName), TestContext.Current.CancellationToken));

        return person;
    }

    public static string PersonToken(TestUser person) => TestTokenIssuer.ForPerson(person, TestApplications.NativeClient, BearerTokenRoutes.ReadScope);

    private static ErpRole[] RolesOf(HttpContext context) => [.. CallerRoles.Of(context).Order()];
}
