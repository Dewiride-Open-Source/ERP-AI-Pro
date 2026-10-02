using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.BuildingBlocks.Caching.Persistence;

// The table the dotnet-sql-cache tool creates, column for column: SqlServerCache sizes its key parameter to 449 characters,
// compares keys case-sensitively, and treats only a primary key violation (error 2627) on insert as a lost upsert race.
internal sealed class DistributedCacheEntryConfiguration : IEntityTypeConfiguration<DistributedCacheEntry>
{
    public const int IdMaxLength = 449;

    public const string IdCollation = "SQL_Latin1_General_CP1_CS_AS";

    public const string ExpiresAtTimeIndex = "Index_ExpiresAtTime";

    public void Configure(EntityTypeBuilder<DistributedCacheEntry> builder)
    {
        builder.ToTable(CachingDbContext.DistributedCacheTable);
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasMaxLength(IdMaxLength).IsUnicode().UseCollation(IdCollation).ValueGeneratedNever();
        builder.Property(entry => entry.Value).IsRequired();
        builder.HasIndex(entry => entry.ExpiresAtTime).HasDatabaseName(ExpiresAtTimeIndex);
    }
}
