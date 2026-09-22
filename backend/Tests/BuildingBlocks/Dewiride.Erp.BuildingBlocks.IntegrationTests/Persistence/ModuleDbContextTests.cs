using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class ModuleDbContextTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    [Fact]
    public async Task Migrate_SampleContext_CreatesTheSchemaWithTheHistoryTableAndTheTablesInsideIt()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var schemas = await context.Database
            .SqlQueryRaw<string>("SELECT s.name AS [Value] FROM sys.schemas AS s WHERE s.name = {0}", SampleDbContext.SchemaName)
            .ToListAsync(TestContext.Current.CancellationToken);
        var tables = await context.Database
            .SqlQueryRaw<string>("SELECT t.TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES AS t WHERE t.TABLE_SCHEMA = {0}", SampleDbContext.SchemaName)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([SampleDbContext.SchemaName], schemas);
        Assert.Equal(["SampleLines", "Samples", ModuleDbContextRegistration.MigrationsHistoryTable], tables.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SaveChanges_StronglyTypedIdAndComplexType_RoundTripThroughTheExpectedColumns()
    {
        var id = SampleId.Create();
        var occurredAt = new DateTimeOffset(2026, 4, 1, 9, 30, 0, TimeSpan.FromHours(5.5));

        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            context.Samples.Add(new SampleAggregate(id, "Round trip", new Money(1234.5678m, Currency.Inr), new SampleAddress("12 MG Road", "Jaipur"), occurredAt));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var sample = await context.Samples.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
            var columns = await context.Database
                .SqlQueryRaw<ColumnShape>(
                    "SELECT c.COLUMN_NAME AS [Name], c.DATA_TYPE AS [DataType], c.CHARACTER_MAXIMUM_LENGTH AS [MaxLength], c.NUMERIC_PRECISION AS [Precision], c.NUMERIC_SCALE AS [Scale] FROM INFORMATION_SCHEMA.COLUMNS AS c WHERE c.TABLE_SCHEMA = {0} AND c.TABLE_NAME = 'Samples'",
                    SampleDbContext.SchemaName)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(id, sample.Id);
            Assert.Equal("Round trip", sample.Name);
            Assert.Equal(new Money(1234.5678m, Currency.Inr), sample.Price);
            Assert.Equal(new SampleAddress("12 MG Road", "Jaipur"), sample.Address);
            Assert.Equal(occurredAt, sample.OccurredAt);
            Assert.Equal(TimeSpan.Zero, sample.OccurredAt.Offset);
            Assert.Equal(
                [
                    new ColumnShape("Address_City", "nvarchar", 50, null, null),
                    new ColumnShape("Address_Line1", "nvarchar", 100, null, null),
                    new ColumnShape("CreatedAt", "datetimeoffset", null, null, null),
                    new ColumnShape("CreatedBy", "uniqueidentifier", null, null, null),
                    new ColumnShape("DeletedAt", "datetimeoffset", null, null, null),
                    new ColumnShape("DeletedBy", "uniqueidentifier", null, null, null),
                    new ColumnShape("Id", "uniqueidentifier", null, null, null),
                    new ColumnShape("IsDeleted", "bit", null, null, null),
                    new ColumnShape("ModifiedAt", "datetimeoffset", null, null, null),
                    new ColumnShape("ModifiedBy", "uniqueidentifier", null, null, null),
                    new ColumnShape("Name", "nvarchar", 50, null, null),
                    new ColumnShape("OccurredAt", "datetimeoffset", null, null, null),
                    new ColumnShape("Price_Amount", "decimal", null, 19, 4),
                    new ColumnShape("Price_Currency", "char", 3, null, null),
                    new ColumnShape("RowVersion", "timestamp", null, null, null),
                ],
                columns.OrderBy(c => c.Name, StringComparer.Ordinal));
        }
    }

    [Fact]
    public async Task SaveChanges_DateTimeOffsetWithAnOffset_IsStoredWithAZeroOffset()
    {
        var id = SampleId.Create();

        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            context.Samples.Add(new SampleAggregate(id, "Offset", new Money(1m, Currency.Inr), new SampleAddress("1", "Pune"), new DateTimeOffset(2026, 4, 1, 0, 15, 0, TimeSpan.FromHours(5.5))));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = database.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var stored = await context.Database
                .SqlQueryRaw<StoredOffset>("SELECT DATEPART(tz, s.OccurredAt) AS [OffsetMinutes], CONVERT(nvarchar(33), s.OccurredAt, 127) AS [Text] FROM [test_sample].[Samples] AS s WHERE s.Id = {0}", id.Value)
                .SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, stored.OffsetMinutes);
            Assert.StartsWith("2026-03-31T18:45:00", stored.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Model_StronglyTypedIdProperty_UsesTheConverter()
    {
        using var context = new SampleDbContext(SampleDbContextDesignTimeFactory.Options(BuildingBlocks.Persistence.Options.DatabaseProvider.SqlServer));

        var property = context.Model.FindEntityType(typeof(SampleAggregate))!.FindProperty(nameof(SampleAggregate.Id))!;

        Assert.IsType<StronglyTypedIdConverter<SampleId>>(property.GetValueConverter());
        Assert.Equal(typeof(Guid), property.GetProviderClrType() ?? property.GetValueConverter()?.ProviderClrType);
    }

    private sealed record ColumnShape(string Name, string DataType, int? MaxLength, byte? Precision, int? Scale);

    private sealed record StoredOffset(int OffsetMinutes, string Text);
}
