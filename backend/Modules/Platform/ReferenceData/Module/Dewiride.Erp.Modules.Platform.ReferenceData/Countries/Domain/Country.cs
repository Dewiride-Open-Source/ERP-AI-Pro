using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;

// The countries are the seed file's: the seeder adds each one and changes a name only when the file does, and no person
// creates, renames or deletes one, so a malformed value is a defect of the file and throws.
internal sealed class Country : AggregateRoot<CountryId>, IAuditable
{
    public const int CodeLength = 2;

    public const int NameMaxLength = 100;

    private Country(CountryId id)
        : base(id)
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static bool IsCode(string? code) => code is { Length: CodeLength } && code.All(char.IsAsciiLetterUpper);

    public static bool IsName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= NameMaxLength && string.Equals(name, name.Trim(), StringComparison.Ordinal);

    public static Country Create(string code, string name)
    {
        if (!IsCode(code))
        {
            throw new ArgumentException($"'{code}' is not two capital letters.", nameof(code));
        }

        var country = new Country(CountryId.Create()) { Code = code };
        country.Rename(name);

        return country;
    }

    public void Rename(string name)
    {
        if (!IsName(name))
        {
            throw new ArgumentException($"'{name}' is not a country name of 1 to {NameMaxLength} characters without spaces at either end.", nameof(name));
        }

        Name = name;
    }
}
