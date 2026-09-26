using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

internal static class TestKeys
{
    // The leading digits of a version 7 GUID are its timestamp; only the trailing part is random.
    public static string NewId() => $"key-{Guid.CreateVersion7().ToString("N")[20..]}";

    public static EncryptionKey Create() => Create(NewId());

    public static EncryptionKey Create(string id) => new(id, RandomNumberGenerator.GetBytes(EncryptionKey.MaterialLength));

    public static string Setting(EncryptionKey key) => Setting(key.Id, key.Material);

    public static string Setting(string id, byte[] material) => $"{id}{EncryptionKeys.IdSeparator}{Convert.ToBase64String(material)}";

    public static string RetiredSetting(params EncryptionKey[] keys) => string.Join(EncryptionKeys.ListSeparator, keys.Select(Setting));
}
