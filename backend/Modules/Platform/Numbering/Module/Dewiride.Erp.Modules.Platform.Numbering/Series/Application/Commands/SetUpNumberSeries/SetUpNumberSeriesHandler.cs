using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Commands.SetUpNumberSeries;

internal sealed class SetUpNumberSeriesHandler(INumberingAdministration administration) : ICommandHandler<SetUpNumberSeriesCommand, NumberedDocumentDetails>
{
    public Task<Result<NumberedDocumentDetails>> HandleAsync(SetUpNumberSeriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return administration.SetUpAsync(command.DocumentType, command.Format, command.NextNumber, cancellationToken);
    }
}
