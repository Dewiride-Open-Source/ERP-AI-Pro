using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Testing;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IntegrationTests.ReferenceLists;

public sealed class ReferenceDataPermissionTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Theory]
    [InlineData(ReferenceDataPermissions.ReadExchangeRates)]
    [InlineData(ReferenceDataPermissions.RecordExchangeRates)]
    [InlineData(ReferenceDataPermissions.CorrectExchangeRates)]
    public void PermissionsOf_Roles_GiveAnExchangeRatePermissionToFinanceAndAdministratorsAlone(string permission)
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();

        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Finance]));
        Assert.Contains(permission, catalog.PermissionsOf([ErpRole.Administrator]));
        Assert.DoesNotContain(permission, catalog.PermissionsOf([ErpRole.Employee, ErpRole.Hr, ErpRole.Manager]));
    }

    [Fact]
    public void PermissionsOf_Employee_GiveTheListsToEveryPerson()
    {
        var catalog = factory.Services.GetRequiredService<PermissionCatalog>();

        Assert.Contains(ReferenceDataPermissions.ReadLists, catalog.PermissionsOf([ErpRole.Employee]));
    }
}
