using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Persistence;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.UnitTests.Countries.Persistence;

public sealed class CountrySourceFileTests
{
    private static readonly CountrySourceFile File = CountrySourceFile.Read();

    [Fact]
    public void Read_SeedFile_HoldsTheIsoCodesAndKosovoOnceEach()
    {
        Assert.Equal(250, File.Countries.Count);
        Assert.Equal(250, File.Countries.Select(country => country.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(File.Countries.Select(country => country.Code).Order(StringComparer.Ordinal), File.Countries.Select(country => country.Code));
        Assert.False(string.IsNullOrWhiteSpace(File.Source));
    }

    [Theory]
    [InlineData("IN", "India")]
    [InlineData("US", "United States of America")]
    [InlineData("GB", "United Kingdom of Great Britain and Northern Ireland")]
    [InlineData("AE", "United Arab Emirates")]
    [InlineData("TR", "Türkiye")]
    [InlineData("CI", "Côte d'Ivoire")]
    [InlineData("KP", "Korea (Democratic People's Republic of)")]
    [InlineData("XK", "Kosovo")]
    public void Read_SeedFile_NamesTheCountryOfTheCode(string code, string name)
    {
        Assert.Equal(name, Assert.Single(File.Countries, country => country.Code == code).Name);
    }
}
