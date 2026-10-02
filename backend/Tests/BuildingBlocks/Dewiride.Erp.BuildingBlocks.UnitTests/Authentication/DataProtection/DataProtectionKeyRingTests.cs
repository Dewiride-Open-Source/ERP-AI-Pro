using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.DataProtection;

public sealed class DataProtectionKeyRingTests
{
    [Fact]
    public void DefaultDirectory_IsTheProductFolderUnderLocalApplicationData()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);

        Assert.True(Path.IsPathFullyQualified(DataProtectionKeyRing.DefaultDirectory), DataProtectionKeyRing.DefaultDirectory);
        Assert.Equal(Path.Combine(localApplicationData, "ERP-AI-Pro", "DataProtection-Keys"), DataProtectionKeyRing.DefaultDirectory);
    }

    [Fact]
    public void DefaultDirectory_IsNotTheFolderEveryAspNetCoreAppOfTheUserShares()
    {
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}ASP.NET{Path.DirectorySeparatorChar}", DataProtectionKeyRing.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}.aspnet{Path.DirectorySeparatorChar}", DataProtectionKeyRing.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplicationName_IsTheFixedDiscriminator()
    {
        Assert.Equal("Dewiride.Erp", DataProtectionKeyRing.ApplicationName);
    }
}
