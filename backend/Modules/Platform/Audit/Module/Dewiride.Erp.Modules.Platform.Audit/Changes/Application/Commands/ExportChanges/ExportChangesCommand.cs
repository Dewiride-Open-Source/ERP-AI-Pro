using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Application.Commands.ExportChanges;

internal sealed record ExportChangesCommand(FilterRequest Filter) : ICommand<ChangeExport>;
