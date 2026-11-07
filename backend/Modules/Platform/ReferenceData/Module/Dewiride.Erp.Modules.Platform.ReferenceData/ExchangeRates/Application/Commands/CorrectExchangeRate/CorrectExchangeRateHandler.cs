using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Application.Commands.CorrectExchangeRate;

// The version the correction was made from becomes the original rowversion, so saving refuses a correction of a rate that
// changed since it was read.
internal sealed class CorrectExchangeRateHandler(ReferenceDataDbContext context) : ICommandHandler<CorrectExchangeRateCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CorrectExchangeRateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!RecordVersion.TryDecode(command.Version, out var version))
        {
            return ExchangeRateErrors.VersionInvalid;
        }

        var id = ExchangeRateId.From(command.Id);
        var rate = await context.ExchangeRates.SingleOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
        if (rate is null)
        {
            return ExchangeRateErrors.NotFound;
        }

        context.Entry(rate).Property(e => e.RowVersion).OriginalValue = version;
        var corrected = rate.Correct(command.Rate, command.Unit, command.Source);

        return corrected.IsFailure ? corrected.Error! : rate.Id.Value;
    }
}
