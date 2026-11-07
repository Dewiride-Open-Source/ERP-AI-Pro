using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;

// A GST State code stands for one territory over a span of dates (Daman and Diu's 25 ended on 31 July 2020, and 26 took its
// merged name on 26 January 2020), so a row is a code from a date. The rows are the seed file's and no person changes one,
// so a malformed value is a defect of the file and throws.
internal sealed class IndianState : AggregateRoot<IndianStateId>, IAuditable
{
    public const int GstCodeLength = 2;

    public const int NameMaxLength = 100;

    public const int SourceMaxLength = 400;

    private IndianState(IndianStateId id)
        : base(id)
    {
    }

    public string GstCode { get; private set; } = string.Empty;

    public DateOnly ValidFrom { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public TerritoryKind Kind { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static bool IsGstCode(string? code) => code is { Length: GstCodeLength } && code.All(char.IsAsciiDigit);

    public static IndianState Create(string gstCode, DateOnly validFrom, string name, TerritoryKind kind, DateOnly? validUntil, string source)
    {
        if (!IsGstCode(gstCode))
        {
            throw new ArgumentException($"'{gstCode}' is not a GST State code of two digits.", nameof(gstCode));
        }

        var state = new IndianState(IndianStateId.Create()) { GstCode = gstCode, ValidFrom = validFrom };
        state.Revise(name, kind, validUntil, source);

        return state;
    }

    public void Revise(string name, TerritoryKind kind, DateOnly? validUntil, string source)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength || !string.Equals(name, name.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{name}' is not a name of 1 to {NameMaxLength} characters without spaces at either end.", nameof(name));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind of a State code is a State, a Union Territory or the Other Territory.");
        }

        if (validUntil < ValidFrom)
        {
            throw new ArgumentOutOfRangeException(nameof(validUntil), validUntil, $"A State code valid from {ValidFrom:yyyy-MM-dd} cannot end before it.");
        }

        if (string.IsNullOrWhiteSpace(source) || source.Length > SourceMaxLength)
        {
            throw new ArgumentException($"The instrument of State code {GstCode} has 1 to {SourceMaxLength} characters.", nameof(source));
        }

        Name = name;
        Kind = kind;
        ValidUntil = validUntil;
        Source = source;
    }

    public bool AppliesOn(DateOnly date) => date >= ValidFrom && (ValidUntil is not { } until || date <= until);
}
