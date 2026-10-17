using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.Audit.Contracts;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints;

public sealed class AuditPermissionTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData(AuditPermissions.ReadChanges)]
    [InlineData(AuditPermissions.ExportChanges)]
    public void PermissionsOf_EveryRoleButAdministrator_LeaveTheAuditPermissionToTheAdministrator(string permission)
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();
        var others = Enum.GetValues<ErpRole>().Where(role => role != ErpRole.Administrator).ToList();

        Assert.True(catalog.Contains(permission));
        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Administrator]));
        Assert.NotEmpty(others);
        Assert.All(others, role => Assert.DoesNotContain(permission, catalog.PermissionsOf([role])));
        Assert.DoesNotContain(permission, catalog.PermissionsOf(others));
    }
}
