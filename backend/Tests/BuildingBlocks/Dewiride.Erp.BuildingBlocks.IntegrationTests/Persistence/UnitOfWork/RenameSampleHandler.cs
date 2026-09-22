using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.UnitOfWork;

public sealed class RenameSampleHandler(SampleDbContext context, TimeProvider timeProvider) : ICommandHandler<RenameSampleCommand, string>
{
    public const string MissingCode = "sample.missing";

    public const string RefusedCode = "sample.rename-refused";

    public async Task<Result<string>> HandleAsync(RenameSampleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var sample = await context.Samples.SingleOrDefaultAsync(s => s.Id == command.SampleId, cancellationToken);
        if (sample is null)
        {
            return Error.NotFound(MissingCode, $"Sample {command.SampleId.Value} does not exist.");
        }

        var oldName = sample.Name;
        sample.Rename(command.NewName, timeProvider.GetUtcNow());

        return command.Outcome switch
        {
            RenameOutcome.FailAfterRenaming => Error.Conflict(RefusedCode, "The rename was refused after the change was made."),
            RenameOutcome.ThrowAfterRenaming => throw new InvalidOperationException("The rename threw after the change was made."),
            _ => oldName,
        };
    }
}
