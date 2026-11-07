using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests;

public sealed class StatutoryParametersModuleTests
{
    [Fact]
    public void Descriptor_Permissions_AreTheOneToReadTheSetsHeldByFinance()
    {
        var declared = Assert.Single(new StatutoryParametersModule().Descriptor.Permissions);

        Assert.Equal(StatutoryParametersPermissions.ReadParameterSets, declared.Name);
        Assert.Equal([ErpRole.Finance], declared.HeldBy);
        Assert.Equal("See the government rates, codes and due dates the ERP uses, and their history.", declared.Description);
    }

    [Fact]
    public void Descriptor_Names_FollowTheModule()
    {
        var descriptor = new StatutoryParametersModule().Descriptor;

        Assert.Equal(("Compliance", "StatutoryParameters", "compliance_statutory_parameters", "/compliance/statutory-parameters", StatutoryParametersModule.FeatureFlag),
            (descriptor.Domain, descriptor.Name, descriptor.Schema, descriptor.RoutePrefix, descriptor.FeatureFlag));
        Assert.Equal("Erp.Modules.Compliance.StatutoryParameters", StatutoryParametersModule.FeatureFlag);
    }
}
