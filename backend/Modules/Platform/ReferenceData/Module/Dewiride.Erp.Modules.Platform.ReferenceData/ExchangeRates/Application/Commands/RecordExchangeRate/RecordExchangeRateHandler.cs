using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Monetary;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.RecordExchangeRate;

internal sealed class RecordExchangeRateHandler(ReferenceDataDbContext context, TimeProvider timeProvider) : ICommandHandler<RecordExchangeRateCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RecordExchangeRateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Currency.TryFromCode(command.Currency, out var currency))
        {
            return ExchangeRateErrors.CurrencyUnknown(command.Currency);
        }

        var recorded = ExchangeRate.Record(currency, command.RateDate, command.Rate, command.Unit, command.Source, IndianStandardTime.Today(timeProvider));
        if (recorded.IsFailure)
        {
            return recorded.Error!;
        }

        if (await context.ExchangeRates.AnyAsync(e => e.Currency == currency && e.RateDate == command.RateDate, cancellationToken).ConfigureAwait(false))
        {
            return ExchangeRateErrors.AlreadyRecorded(currency, command.RateDate);
        }

        context.ExchangeRates.Add(recorded.Value);

        return recorded.Value.Id.Value;
    }
}
