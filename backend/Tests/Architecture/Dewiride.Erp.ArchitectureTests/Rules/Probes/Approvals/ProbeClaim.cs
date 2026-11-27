using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.ArchitectureTests.Rules.Probes.Approvals;

public sealed class ProbeClaim : IAuditable
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedBy { get; set; }
}
