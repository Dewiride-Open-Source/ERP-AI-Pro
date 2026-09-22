using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.Conventions;

public sealed class CurrencyConverterTests
{
    [Fact]
    public void Converter_RoundTrip_StoresTheCode()
    {
        var converter = new CurrencyConverter();

        var stored = converter.ConvertToProviderTyped(Currency.Jpy);
        var restored = converter.ConvertFromProviderTyped(stored);

        Assert.Equal("JPY", stored);
        Assert.Equal(Currency.Jpy, restored);
    }

    [Fact]
    public void ConvertFromProvider_CodeOutsideTheTable_ThrowsNamingTheCode()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CurrencyConverter().ConvertFromProviderTyped("XXX"));

        Assert.Contains("XXX", exception.Message, StringComparison.Ordinal);
    }
}
