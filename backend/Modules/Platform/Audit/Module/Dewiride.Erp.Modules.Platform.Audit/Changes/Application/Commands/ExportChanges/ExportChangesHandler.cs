using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Application.Commands.ExportChanges;

// The export is recorded before its first line is written, and an export whose record cannot be written is not made: the
// failure is thrown, so the request answers 500 without a byte of the file. A refused export is no export and records nothing.
internal sealed class ExportChangesHandler(IChangeHistory history, ISecurityEventRecorder securityEvents, IActorContext actor, IRequestOrigin origin)
    : ICommandHandler<ExportChangesCommand, ChangeExport>
{
    public async Task<Result<ChangeExport>> HandleAsync(ExportChangesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var export = await history.ExportAsync(command.Filter, cancellationToken).ConfigureAwait(false);
        if (export.IsFailure)
        {
            return export.Error!;
        }

        await securityEvents.RecordAsync(
            new SecurityEventEntry(
                SecurityEventKind.ChangeHistoryExported,
                Detail: string.Create(CultureInfo.InvariantCulture, $"records:{export.Value.RecordCount}"),
                ActorObjectId: actor.IsAuthenticated ? actor.ActorId : null,
                ClientAddress: origin.ClientAddress,
                CorrelationId: origin.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        return export.Value;
    }
}
