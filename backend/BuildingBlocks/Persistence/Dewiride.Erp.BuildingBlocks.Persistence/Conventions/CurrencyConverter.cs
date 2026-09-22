using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Conventions;

public sealed class CurrencyConverter : ValueConverter<Currency, string>
{
    public CurrencyConverter()
        : base(currency => currency.Code, code => Currency.FromCode(code))
    {
    }
}
