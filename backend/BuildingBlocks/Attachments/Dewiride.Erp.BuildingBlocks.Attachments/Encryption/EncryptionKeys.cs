using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

internal static class EncryptionKeys
{
    public const char IdSeparator = ':';

    public const char ListSeparator = ';';

    private const string Format = "'<key id>:<32 random bytes in base64>'";

    public static EncryptionKeySet Parse(string? current, string? retired) =>
        TryParse(current, retired, out var keys, out var problem) ? keys : throw new InvalidOperationException(problem);

    // Messages name the setting and the key id, never the key material.
    public static bool TryParse(string? current, string? retired, [NotNullWhen(true)] out EncryptionKeySet? keys, [NotNullWhen(false)] out string? problem)
    {
        keys = null;
        if (string.IsNullOrWhiteSpace(current))
        {
            problem = $"{AttachmentsOptions.SectionName}:EncryptionKey is required: set it to {Format}. It comes from the Key Vault reference " +
                "Erp--Platform--Attachments--EncryptionKey through App Configuration, from dotnet user-secrets on a developer machine, from the environment " +
                "variable Erp__Platform__Attachments__EncryptionKey in CI, or from the secret file /run/secrets/Erp__Platform__Attachments__EncryptionKey in a container.";
            return false;
        }

        if (!TryParseKey(current, out var currentKey))
        {
            problem = $"{AttachmentsOptions.SectionName}:EncryptionKey is not {Format}.";
            return false;
        }

        var retiredKeys = new List<EncryptionKey>();
        var entries = (retired ?? string.Empty).Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < entries.Length; index++)
        {
            if (!TryParseKey(entries[index], out var retiredKey))
            {
                problem = $"{AttachmentsOptions.SectionName}:RetiredEncryptionKeys entry {index + 1} is not {Format}; separate entries with '{ListSeparator}'.";
                return false;
            }

            retiredKeys.Add(retiredKey);
        }

        // A rotation first copies the current key into the retired list and only then replaces the current key, so while the
        // two settings refresh one after the other the same key may appear in both; an id naming two different keys is refused.
        var distinct = new List<EncryptionKey>();
        foreach (var key in retiredKeys.Prepend(currentKey))
        {
            var existing = distinct.Find(candidate => string.Equals(candidate.Id, key.Id, StringComparison.Ordinal));
            if (existing is null)
            {
                distinct.Add(key);
            }
            else if (!CryptographicOperations.FixedTimeEquals(existing.Material, key.Material))
            {
                problem = $"The attachment encryption key id '{key.Id}' names two different keys across EncryptionKey and RetiredEncryptionKeys.";
                return false;
            }
        }

        keys = new EncryptionKeySet(currentKey, distinct.Skip(1).ToList());
        problem = null;
        return true;
    }

    private static bool TryParseKey(string value, [NotNullWhen(true)] out EncryptionKey? key)
    {
        key = null;
        var separator = value.IndexOf(IdSeparator, StringComparison.Ordinal);
        if (separator <= 0 || !EncryptionKey.IsValidId(value.AsSpan(0, separator)))
        {
            return false;
        }

        var material = new byte[EncryptionKey.MaterialLength];
        if (!Convert.TryFromBase64String(value[(separator + 1)..].Trim(), material, out var written) || written != EncryptionKey.MaterialLength)
        {
            return false;
        }

        key = new EncryptionKey(value[..separator], material);
        return true;
    }
}
