namespace Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

internal sealed class EncryptionKey
{
    public const int MaterialLength = 32;

    public const int MaxIdLength = 32;

    public EncryptionKey(string id, byte[] material)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentOutOfRangeException.ThrowIfNotEqual(material.Length, MaterialLength, nameof(material));

        Id = id;
        Material = material;
    }

    public string Id { get; }

    public byte[] Material { get; }

    public static bool IsValidId(ReadOnlySpan<char> id)
    {
        if (id.IsEmpty || id.Length > MaxIdLength)
        {
            return false;
        }

        foreach (var character in id)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
