using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Allocation;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Numbering;

// No module issues documents yet, so a test takes numbers as an issuing command would, in a unit of work on a context that maps
// the numbering tables, the numbering's own, dated today in India; the numbers commit together, and none when one is refused.
public static class NumberIssuer
{
    public static async Task<Result<IReadOnlyList<DocumentNumber>>> IssueAsync(IServiceProvider services, string documentType, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NumberingDbContext>();
        var numbers = scope.ServiceProvider.GetRequiredService<IDocumentNumbers>();
        var today = IndianStandardTime.Today(scope.ServiceProvider.GetRequiredService<TimeProvider>());

        return await EfUnitOfWork.RunAsync<IReadOnlyList<DocumentNumber>>(
            context,
            scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>(),
            scope.ServiceProvider.GetRequiredService<UnitOfWorkSignal>(),
            async token =>
            {
                var issued = new List<DocumentNumber>(count);
                for (var issue = 0; issue < count; issue++)
                {
                    var number = await numbers.NextAsync(context, documentType, today, token).ConfigureAwait(false);
                    if (number.IsFailure)
                    {
                        return number.Error!;
                    }

                    issued.Add(number.Value);
                }

                return issued;
            },
            cancellationToken).ConfigureAwait(false);
    }
}
