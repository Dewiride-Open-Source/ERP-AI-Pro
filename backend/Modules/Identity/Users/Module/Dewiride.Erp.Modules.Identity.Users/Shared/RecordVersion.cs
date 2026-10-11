namespace Dewiride.Erp.Modules.Identity.Users.Shared;

// The version is the record's SQL Server rowversion, eight bytes, carried as Base64 so that an edit names the state it was
// made from.
internal static class RecordVersion
{
    public const int InputMaxLength = 64;

    private const int Length = 8;

    public static string Encode(byte[] rowVersion)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);

        return Convert.ToBase64String(rowVersion);
    }

    public static bool TryDecode(string? version, out byte[] rowVersion)
    {
        rowVersion = new byte[Length];

        return version is not null && Convert.TryFromBase64String(version, rowVersion, out var written) && written == Length;
    }
}
