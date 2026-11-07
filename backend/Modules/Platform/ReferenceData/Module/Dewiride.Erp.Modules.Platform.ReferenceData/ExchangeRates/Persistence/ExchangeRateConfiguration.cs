using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Persistence;

// One rate per currency and day among the rates not removed; a removed rate keeps its row, so the day can be recorded again.
internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");
        builder.RecordChangesAs("platform.reference-data.exchange-rates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Source).HasMaxLength(ExchangeRate.SourceMaxLength);
        builder.HasIndex(e => new { e.Currency, e.RateDate }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.RateDate);
    }
}
