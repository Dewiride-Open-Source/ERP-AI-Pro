using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence;

public sealed class MoneyMappingTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    [Fact]
    public async Task SaveChangesAsync_MoneyProperty_RoundTripsThroughDecimal19x4AndChar3()
    {
        var id = SampleId.Create();
        await database.AddAsync(new SampleAggregate(id, "Priced", new Money(99999.1234m, Currency.Jpy), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));

        var sample = await database.FindAsync(id);
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var stored = await context.Database
            .SqlQueryRaw<StoredMoney>("SELECT s.Price_Amount AS [Amount], s.Price_Currency AS [Currency] FROM [test_sample].[Samples] AS s WHERE s.Id = {0}", id.Value)
            .SingleAsync(TestContext.Current.CancellationToken);
        var columns = await context.Database
            .SqlQueryRaw<ColumnShape>("SELECT c.COLUMN_NAME AS [Name], c.DATA_TYPE AS [DataType], c.CHARACTER_MAXIMUM_LENGTH AS [MaxLength], c.NUMERIC_PRECISION AS [Precision], c.NUMERIC_SCALE AS [Scale] FROM INFORMATION_SCHEMA.COLUMNS AS c WHERE c.TABLE_SCHEMA = {0} AND c.TABLE_NAME = 'Samples' AND c.COLUMN_NAME LIKE 'Price[_]%'", SampleDbContext.SchemaName)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Money(99999.1234m, Currency.Jpy), sample.Price);
        Assert.Equal(99999.1234m, stored.Amount);
        Assert.Equal("JPY", stored.Currency);
        Assert.Equal(
            [new ColumnShape("Price_Amount", "decimal", null, 19, 4), new ColumnShape("Price_Currency", "char", 3, null, null)],
            columns.OrderBy(c => c.Name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Query_MoneyAmount_TranslatesToSql()
    {
        var cheap = SampleId.Create();
        var dear = SampleId.Create();
        await database.AddAsync(new SampleAggregate(cheap, "Cheap", new Money(10m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));
        await database.AddAsync(new SampleAggregate(dear, "Dear", new Money(1000m, Currency.Inr), new SampleAddress("1", "Pune"), database.Clock.GetUtcNow()));
        SampleId[] ids = [cheap, dear];

        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var expensive = await context.Samples
            .Where(s => ids.Contains(s.Id) && s.Price.Amount > 100m && s.Price.Currency == Currency.Inr)
            .Select(s => s.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([dear], expensive);
    }

    private sealed record StoredMoney(decimal Amount, string Currency);

    private sealed record ColumnShape(string Name, string DataType, int? MaxLength, byte? Precision, int? Scale);
}
