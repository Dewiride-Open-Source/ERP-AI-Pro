using System.Diagnostics.CodeAnalysis;

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

        var duplicate = retiredKeys.Prepend(currentKey).GroupBy(key => key.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            problem = $"The attachment encryption key id '{duplicate.Key}' appears more than once across EncryptionKey and RetiredEncryptionKeys.";
            return false;
        }

        keys = new EncryptionKeySet(currentKey, retiredKeys);
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
