using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.UnitOfWork;

public sealed class SampleRenamedHandler(SampleDbContext context) : IDomainEventHandler<SampleRenamed>
{
    public const string PoisonName = "poison";

    public async Task HandleAsync(SampleRenamed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (domainEvent.NewName == PoisonName)
        {
            throw new InvalidOperationException("The rename event handler refused the poison name.");
        }

        var sample = await context.Samples.SingleAsync(s => s.Id == domainEvent.SampleId, cancellationToken);
        sample.AddLine($"renamed {domainEvent.OldName} to {domainEvent.NewName}");
    }
}
