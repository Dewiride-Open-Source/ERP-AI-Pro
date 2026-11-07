using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.RemoveExchangeRate;

// Exchange rates are master data, so removing one soft-deletes it: its row and its change history stay, and its currency and
// day can be recorded again.
internal sealed class RemoveExchangeRateHandler(ReferenceDataDbContext context) : ICommandHandler<RemoveExchangeRateCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RemoveExchangeRateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = ExchangeRateId.From(command.Id);
        var rate = await context.ExchangeRates.SingleOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
        if (rate is null)
        {
            return ExchangeRateErrors.NotFound;
        }

        context.ExchangeRates.Remove(rate);

        return rate.Id.Value;
    }
}
