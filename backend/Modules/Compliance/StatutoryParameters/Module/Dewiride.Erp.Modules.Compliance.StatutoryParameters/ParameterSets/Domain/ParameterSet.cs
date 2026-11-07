using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;

// A set of statutory parameters is a seed file's, known by its key, and no person changes one: a release changes the file and
// the seeder follows it, so a malformed value is a defect of the file and throws. The key, two or more dot-separated parts of
// lower-case words joined by hyphens, is the file's name and the set's address, and never changes.
internal sealed partial class ParameterSet : AggregateRoot<ParameterSetId>, IAuditable
{
    public const int KeyMaxLength = 64;

    public const string KeyPattern = "^" + KeyPatternBody + "$";

    public const int TitleMaxLength = 200;

    public const int DescriptionMaxLength = 1000;

    private const string KeyPatternBody = @"[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+(?:-[a-z0-9]+)*)+";

    private ParameterSet(ParameterSetId id)
        : base(id)
    {
    }

    public string Key { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public SetBasis Basis { get; private set; }

    public AsOfDate AsOf { get; private set; }

    public ParameterColumnSet Columns { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static bool IsKey(string? key) => key is { Length: <= KeyMaxLength } && KeyRegex().IsMatch(key);

    public static ParameterSet Create(string key, string title, string description, SetBasis basis, AsOfDate asOf, ParameterColumnSet columns)
    {
        if (!IsKey(key))
        {
            throw new ArgumentException($"'{key}' is not a set key of at most {KeyMaxLength} characters: two or more dot-separated parts of lower-case words joined by hyphens.", nameof(key));
        }

        var set = new ParameterSet(ParameterSetId.Create()) { Key = key };
        set.Revise(title, description, basis, asOf, columns);

        return set;
    }

    public void Revise(string title, string description, SetBasis basis, AsOfDate asOf, ParameterColumnSet columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        if (!Enum.IsDefined(basis))
        {
            throw new ArgumentOutOfRangeException(nameof(basis), basis, "A set is the law's or the ERP's reading of it.");
        }

        if (!Enum.IsDefined(asOf))
        {
            throw new ArgumentOutOfRangeException(nameof(asOf), asOf, "A set answers for the date of a supply or the first day of a tax period.");
        }

        Title = ParameterText.Require(title, TitleMaxLength, $"The title of set {Key}");
        Description = ParameterText.Require(description, DescriptionMaxLength, $"The description of set {Key}");
        Basis = basis;
        AsOf = asOf;
        Columns = columns;
    }

    [GeneratedRegex("^" + KeyPatternBody + @"\z")]
    private static partial Regex KeyRegex();
}
