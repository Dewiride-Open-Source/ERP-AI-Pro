using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Numbering;

// A retried test meets the numbering its earlier attempt left, so a test removes its kind's numbering, years first, before it
// starts; no person ever removes one.
public static class NumberingResets
{
    public static async Task ResetAsync(IServiceProvider services, string documentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NumberingDbContext>();
        await context.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                var series = await context.Series.Where(s => s.DocumentType == documentType).ToListAsync(token).ConfigureAwait(false);
                var seriesIds = series.Select(s => s.Id).ToList();
                context.Years.RemoveRange(await context.Years.Where(y => seriesIds.Contains(y.SeriesId)).ToListAsync(token).ConfigureAwait(false));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                context.Series.RemoveRange(series);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
