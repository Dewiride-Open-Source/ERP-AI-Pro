using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample.Notes;

// Auditable but not soft-deletable, so removing one deletes its row, and filed under its sample, so its changes show in the
// sample's history.
public sealed class SampleNote : AggregateRoot<SampleNoteId>, IAuditable
{
    public const int TextMaxLength = 200;

    private List<string> _tags = [];

    private SampleNote(SampleNoteId id)
        : base(id)
    {
    }

    public SampleNote(SampleNoteId id, SampleId sampleId, string text, DateOnly? dueOn, IEnumerable<string> tags)
        : base(id)
    {
        SampleId = sampleId;
        Text = text;
        DueOn = dueOn;
        _tags = [.. tags];
    }

    public SampleId SampleId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public DateOnly? DueOn { get; private set; }

    public IReadOnlyList<string> Tags => _tags;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public void Edit(string text, DateOnly? dueOn)
    {
        Text = text;
        DueOn = dueOn;
    }

    public void Retag(IEnumerable<string> tags) => _tags = [.. tags];
}
