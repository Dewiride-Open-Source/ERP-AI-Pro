using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Approvals.Sample;

// A record that needs approval, as a module's expense claim does: submitted in the command that sends it, moved by the outcome of
// its request and approved only with its receipt, so a decision its module refuses can be shown.
public sealed class SampleClaim : AggregateRoot<SampleClaimId>, IAuditable
{
    public const int TitleMaxLength = 100;

    private SampleClaim(SampleClaimId id)
        : base(id)
    {
    }

    public string Title { get; private set; } = string.Empty;

    public Guid RequestedFor { get; private set; }

    public bool ReceiptAttached { get; private set; }

    public SampleClaimState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static SampleClaim Draft(SampleClaimId id, string title, Guid requestedFor, bool receiptAttached) =>
        new(id) { Title = title, RequestedFor = requestedFor, ReceiptAttached = receiptAttached, State = SampleClaimState.Draft };

    public void MoveTo(SampleClaimState state) => State = state;
}
