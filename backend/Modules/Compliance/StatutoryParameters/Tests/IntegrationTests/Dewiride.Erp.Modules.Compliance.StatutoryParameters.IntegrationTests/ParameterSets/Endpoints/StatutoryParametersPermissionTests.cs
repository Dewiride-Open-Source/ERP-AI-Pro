using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.IntegrationTests.ParameterSets.Endpoints;

public sealed class StatutoryParametersPermissionTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public void PermissionsOf_Roles_GiveTheSetsToFinanceAndAdministratorsAlone()
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();

        Assert.Contains(StatutoryParametersPermissions.ReadParameterSets, catalog.PermissionsOf([ErpRole.Finance]));
        Assert.Contains(StatutoryParametersPermissions.ReadParameterSets, catalog.PermissionsOf([ErpRole.Administrator]));
        Assert.DoesNotContain(StatutoryParametersPermissions.ReadParameterSets, catalog.PermissionsOf([ErpRole.Employee, ErpRole.Hr, ErpRole.Manager]));
    }
}
