using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Sorting;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Queries;

public sealed class PagingTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private static readonly SortableFields<SampleAggregate> Sortable = new SortableFields<SampleAggregate>()
        .Add("name", s => s.Name)
        .Add("price", s => s.Price.Amount)
        .Add("occurredAt", s => s.OccurredAt);

    private static readonly FilterableFields<SampleAggregate> Filterable = new FilterableFields<SampleAggregate>()
        .Add("id", s => s.Id)
        .Add("name", s => s.Name)
        .Add("price", s => s.Price.Amount)
        .Add("city", s => s.Address.City)
        .Add("occurredAt", s => s.OccurredAt);

    [Fact]
    public async Task ToPagedResultAsync_SortedAndFilteredList_ReturnsThePageAndTheTotalAcrossPages()
    {
        var stamp = Guid.CreateVersion7().ToString("N")[^12..];
        var ids = new List<SampleId>();
        foreach (var (name, price) in new[] { ("a", 10m), ("b", 30m), ("c", 20m), ("d", 40m), ("e", 50m) })
        {
            ids.Add(await AddAsync($"{stamp}-{name}", price, "Pune"));
        }

        await AddAsync($"{stamp}-z", 60m, "Jaipur");
        var request = ListRequest.Parse(2, 2, "price:desc", $"name:contains:{stamp}-;city:eq:Pune;price:gte:20").Value;

        var page = await QueryAsync(request);

        Assert.Equal([$"{stamp}-b", $"{stamp}-c"], page.Items.Select(s => s.Name));
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.False(page.HasNext);
        Assert.True(page.HasPrevious);
    }

    [Fact]
    public async Task ToPagedResultAsync_PageBeyondTheEnd_ReturnsNoItemsWithoutQueryingRows()
    {
        var stamp = Guid.CreateVersion7().ToString("N")[^12..];
        await AddAsync($"{stamp}-only", 1m, "Pune");

        var page = await QueryAsync(ListRequest.Parse(3, 50, null, $"name:contains:{stamp}").Value);

        Assert.Empty(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task ApplyFilter_StronglyTypedIdsWithInAndAnInstantComparison_TranslateToSql()
    {
        var first = await AddAsync("in-first", 1m, "Pune");
        var second = await AddAsync("in-second", 1m, "Pune");
        await AddAsync("in-third", 1m, "Pune");
        var filter = FilterRequest.Parse($"id:in:{first.Value}|{second.Value};occurredAt:lte:{database.Clock.GetUtcNow():O}").Value.Resolve(Filterable).Value;

        await using var scope = database.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<SampleDbContext>().Samples.ApplyFilter(filter).ApplySort(ResolvedSort<SampleAggregate>.None, s => s.Id);
        var names = await query.Select(s => s.Name).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, names.Count);
        Assert.Contains("[Price_Amount]", query.Where(s => s.Price.Amount > 0).ToQueryString(), StringComparison.Ordinal);
        Assert.DoesNotContain("in-first", query.ToQueryString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplySort_TieBreakerOnly_IsDeterministicAcrossCalls()
    {
        var stamp = Guid.CreateVersion7().ToString("N")[^12..];
        for (var i = 0; i < 3; i++)
        {
            await AddAsync($"{stamp}-same", 5m, "Pune");
        }

        var first = await QueryAsync(ListRequest.Parse(1, 2, "name", $"name:eq:{stamp}-same").Value);
        var second = await QueryAsync(ListRequest.Parse(2, 2, "name", $"name:eq:{stamp}-same").Value);

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(3, first.Items.Select(s => s.Id).Concat(second.Items.Select(s => s.Id)).Distinct().Count());
    }

    private async Task<SampleId> AddAsync(string name, decimal price, string city)
    {
        var id = SampleId.Create();
        await database.AddAsync(new SampleAggregate(id, name, new Money(price, Currency.Inr), new SampleAddress("1", city), database.Clock.GetUtcNow()));

        return id;
    }

    private async Task<PagedResult<SampleAggregate>> QueryAsync(ListRequest request)
    {
        await using var scope = database.CreateScope();
        var samples = scope.ServiceProvider.GetRequiredService<SampleDbContext>().Samples.AsNoTracking();

        return await samples
            .ApplyFilter(request.Filter.Resolve(Filterable).Value)
            .ApplySort(request.Sort.Resolve(Sortable).Value, s => s.Id)
            .ToPagedResultAsync(request.Page, TestContext.Current.CancellationToken);
    }
}
