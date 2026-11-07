using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.UnitTests;

public sealed class ReferenceDataModuleTests
{
    [Theory]
    [InlineData(ReferenceDataPermissions.ReadLists, ErpRole.Employee)]
    [InlineData(ReferenceDataPermissions.ReadExchangeRates, ErpRole.Finance)]
    [InlineData(ReferenceDataPermissions.RecordExchangeRates, ErpRole.Finance)]
    [InlineData(ReferenceDataPermissions.CorrectExchangeRates, ErpRole.Finance)]
    public void Descriptor_Permission_IsHeldByItsRoleBesidesAdministrators(string permission, ErpRole holder)
    {
        var declared = Assert.Single(new ReferenceDataModule().Descriptor.Permissions, declaration => declaration.Name == permission);

        Assert.Equal([holder], declared.HeldBy);
    }

    [Fact]
    public void Descriptor_Permissions_AreTheFourOfTheModule()
    {
        Assert.Equal(
            [ReferenceDataPermissions.ReadLists, ReferenceDataPermissions.ReadExchangeRates, ReferenceDataPermissions.RecordExchangeRates, ReferenceDataPermissions.CorrectExchangeRates],
            new ReferenceDataModule().Descriptor.Permissions.Select(declaration => declaration.Name));
    }
}
