using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

// A version of a set is a legal change: the date from which it applies, known with its set by that date, and the instruments
// that brought it. It runs until the day before the set's next version.
internal sealed class ParameterVersion : AggregateRoot<ParameterVersionId>, IAuditable
{
    public const int InstrumentMaxLength = 400;

    private List<string> _instruments = [];

    private ParameterVersion(ParameterVersionId id)
        : base(id)
    {
    }

    public ParameterSetId SetId { get; private set; }

    public DateOnly From { get; private set; }

    public IReadOnlyList<string> Instruments => _instruments;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static ParameterVersion Create(ParameterSet set, DateOnly from, IEnumerable<string> instruments)
    {
        ArgumentNullException.ThrowIfNull(set);

        var version = new ParameterVersion(ParameterVersionId.Create()) { SetId = set.Id, From = from };
        version.Revise(instruments);

        return version;
    }

    public void Revise(IEnumerable<string> instruments)
    {
        ArgumentNullException.ThrowIfNull(instruments);

        var cited = instruments.ToList();
        if (cited.Count == 0)
        {
            throw new ArgumentException($"The version from {From:yyyy-MM-dd} cites no instrument.", nameof(instruments));
        }

        foreach (var instrument in cited)
        {
            ParameterText.Require(instrument, InstrumentMaxLength, $"An instrument of the version from {From:yyyy-MM-dd}");
        }

        if (cited.Distinct(StringComparer.Ordinal).Count() != cited.Count)
        {
            throw new ArgumentException($"The version from {From:yyyy-MM-dd} cites an instrument twice.", nameof(instruments));
        }

        _instruments = cited;
    }
}
