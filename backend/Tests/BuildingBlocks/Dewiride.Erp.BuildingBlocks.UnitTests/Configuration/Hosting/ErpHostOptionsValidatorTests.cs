using Dewiride.Erp.BuildingBlocks.Configuration.Hosting;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Configuration.Hosting;

public sealed class ErpHostOptionsValidatorTests
{
    [Fact]
    public void Validate_Defaults_Succeed()
    {
        var result = new ErpHostOptionsValidator().Validate(null, new ErpHostOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("172.28.0.0/16")]
    [InlineData("10.0.0.0/8")]
    [InlineData("127.0.0.1/32")]
    [InlineData("::1/128")]
    [InlineData("fd00::/8")]
    public void Validate_NetworkInCidrNotation_Succeeds(string network)
    {
        var result = new ErpHostOptionsValidator().Validate(null, new ErpHostOptions { KnownNetworks = [network] });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("172.28.0.0")]
    [InlineData("172.28.0.5/16")]
    [InlineData("10.0.0.1/8")]
    [InlineData("172.28.0.0/33")]
    [InlineData("172.28.0.0/ 16")]
    [InlineData("not-a-network")]
    [InlineData("")]
    public void Validate_EntryThatIsNotANetwork_FailsNamingTheEntryAndItsIndex(string network)
    {
        var result = new ErpHostOptionsValidator().Validate(null, new ErpHostOptions { KnownNetworks = ["10.0.0.0/8", network] });

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("Erp:Platform:Host:KnownNetworks:1", failure, StringComparison.Ordinal);
        Assert.Contains($"'{network}'", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("erp.example.com;;api.example.com")]
    [InlineData("erp.example.com;")]
    [InlineData(";")]
    public void Validate_AllowedHostsWithAnEmptyEntry_Fails(string allowedHosts)
    {
        var result = new ErpHostOptionsValidator().Validate(null, new ErpHostOptions { AllowedHosts = allowedHosts });

        Assert.True(result.Failed);
        Assert.Contains("AllowedHosts", Assert.Single(result.Failures!), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("erp.example.com")]
    [InlineData("erp.example.com;localhost")]
    public void Validate_AllowedHostsWithoutEmptyEntries_Succeeds(string allowedHosts)
    {
        var result = new ErpHostOptionsValidator().Validate(null, new ErpHostOptions { AllowedHosts = allowedHosts });

        Assert.True(result.Succeeded);
    }
}
