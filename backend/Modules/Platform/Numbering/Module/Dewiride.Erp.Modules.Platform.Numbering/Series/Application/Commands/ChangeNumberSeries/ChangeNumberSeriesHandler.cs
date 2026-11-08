using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;

namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Application.Commands.ChangeNumberSeries;

internal sealed class ChangeNumberSeriesHandler(INumberingAdministration administration) : ICommandHandler<ChangeNumberSeriesCommand, NumberedDocumentDetails>
{
    public Task<Result<NumberedDocumentDetails>> HandleAsync(ChangeNumberSeriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return RecordVersion.TryDecode(command.Version, out var version)
            ? administration.ChangeAsync(command.Id, version, command.Format, command.NextNumber, cancellationToken)
            : Task.FromResult<Result<NumberedDocumentDetails>>(NumberingErrors.VersionInvalid);
    }
}
