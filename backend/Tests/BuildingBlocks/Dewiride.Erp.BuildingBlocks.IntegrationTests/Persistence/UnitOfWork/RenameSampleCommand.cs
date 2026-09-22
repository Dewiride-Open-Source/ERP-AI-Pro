using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.UnitOfWork;

public sealed record RenameSampleCommand(SampleId SampleId, [property: Required][property: StringLength(SampleAggregate.NameMaxLength)] string NewName, RenameOutcome Outcome = RenameOutcome.Succeed) : ICommand<string>;
