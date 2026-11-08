using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Persistence;

internal static class NumberFormatMapping
{
    public static void Configure(ComplexPropertyBuilder<NumberFormat> format)
    {
        format.Property(f => f.Prefix).HasMaxLength(NumberFormat.PrefixMaxLength).IsUnicode(false);
        format.Property(f => f.Year).HasConversion<byte>();
        format.Property(f => f.Separator).HasMaxLength(NumberFormat.SeparatorMaxLength).IsUnicode(false);
    }
}
