using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

public sealed class EncryptionKeysTests
{
    private const string CurrentSetting = $"{AttachmentsOptions.SectionName}:EncryptionKey";

    private const string RetiredSetting = $"{AttachmentsOptions.SectionName}:RetiredEncryptionKeys";

    [Fact]
    public void TryParse_ValidCurrentKey_ReturnsItsIdAndMaterialWithNoRetiredKeys()
    {
        var key = TestKeys.Create();

        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(key), null, out var keys, out var problem);

        Assert.True(parsed);
        Assert.Null(problem);
        Assert.Equal(key.Id, keys!.Current.Id);
        Assert.Equal(key.Material, keys.Current.Material);
        Assert.Empty(keys.Retired);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("key.2026_09-a")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ012345")]
    public void TryParse_KeyIdOfAllowedCharactersUpToThirtyTwo_Succeeds(string id)
    {
        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(id, RandomNumberGenerator.GetBytes(EncryptionKey.MaterialLength)), null, out var keys, out _);

        Assert.True(parsed);
        Assert.Equal(id, keys!.Current.Id);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("  ")]
    [InlineData("\r\n")]
    public void TryParse_MaterialFollowedByWhitespace_Succeeds(string trailing)
    {
        var key = TestKeys.Create();

        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(key) + trailing, null, out var keys, out _);

        Assert.True(parsed);
        Assert.Equal(key.Material, keys!.Current.Material);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_CurrentKeyMissing_FailsNamingTheSettingAndEverySource(string? current)
    {
        var parsed = EncryptionKeys.TryParse(current, null, out var keys, out var problem);

        Assert.False(parsed);
        Assert.Null(keys);
        Assert.Contains($"{CurrentSetting} is required", problem, StringComparison.Ordinal);
        Assert.Contains("Erp--Platform--Attachments--EncryptionKey", problem, StringComparison.Ordinal);
        Assert.Contains("user-secrets", problem, StringComparison.Ordinal);
        Assert.Contains("Erp__Platform__Attachments__EncryptionKey", problem, StringComparison.Ordinal);
        Assert.Contains("/run/secrets/Erp__Platform__Attachments__EncryptionKey", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456")]
    [InlineData("has space")]
    [InlineData("slash/id")]
    [InlineData("plus+id")]
    [InlineData("caf\u00E9")]
    [InlineData(" padded")]
    public void TryParse_MalformedKeyId_FailsNamingTheSettingAndFormat(string id)
    {
        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(id, RandomNumberGenerator.GetBytes(EncryptionKey.MaterialLength)), null, out _, out var problem);

        Assert.False(parsed);
        Assert.Equal($"{CurrentSetting} is not '<key id>:<32 random bytes in base64>'.", problem);
    }

    [Fact]
    public void TryParse_NoIdSeparator_Fails()
    {
        var parsed = EncryptionKeys.TryParse(Convert.ToBase64String(RandomNumberGenerator.GetBytes(EncryptionKey.MaterialLength)), null, out _, out var problem);

        Assert.False(parsed);
        Assert.StartsWith($"{CurrentSetting} is not ", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void TryParse_MaterialNotThirtyTwoBytes_Fails(int length)
    {
        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(TestKeys.NewId(), RandomNumberGenerator.GetBytes(length)), null, out _, out var problem);

        Assert.False(parsed);
        Assert.StartsWith($"{CurrentSetting} is not ", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_MaterialNotBase64_Fails()
    {
        var parsed = EncryptionKeys.TryParse($"{TestKeys.NewId()}:not*base64*material*of*any*kind!", null, out _, out var problem);

        Assert.False(parsed);
        Assert.StartsWith($"{CurrentSetting} is not ", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_SeveralRetiredKeys_ReturnsThemInOrderBehindTheCurrentKey()
    {
        var current = TestKeys.Create();
        var retired = new[] { TestKeys.Create(), TestKeys.Create(), TestKeys.Create() };

        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(current), TestKeys.RetiredSetting(retired), out var keys, out _);

        Assert.True(parsed);
        Assert.Equal(retired.Select(key => key.Id), keys!.Retired.Select(key => key.Id));
        Assert.Equal(retired.Select(key => key.Material), keys.Retired.Select(key => key.Material));
        Assert.Equal([current.Id, .. retired.Select(key => key.Id)], keys.All.Select(key => key.Id));
    }

    [Fact]
    public void TryParse_RetiredListWithEmptyAndBlankEntries_IgnoresThem()
    {
        var first = TestKeys.Create();
        var second = TestKeys.Create();

        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(TestKeys.Create()), $";{TestKeys.Setting(first)};; ;  {TestKeys.Setting(second)}  ;", out var keys, out _);

        Assert.True(parsed);
        Assert.Equal([first.Id, second.Id], keys!.Retired.Select(key => key.Id));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryParse_MalformedRetiredEntry_FailsNamingItsPosition(int position)
    {
        var entries = Enumerable.Range(1, 3)
            .Select(index => index == position ? $"bad id:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(EncryptionKey.MaterialLength))}" : TestKeys.Setting(TestKeys.Create()));

        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(TestKeys.Create()), string.Join(EncryptionKeys.ListSeparator, entries), out var keys, out var problem);

        Assert.False(parsed);
        Assert.Null(keys);
        Assert.Equal($"{RetiredSetting} entry {position} is not '<key id>:<32 random bytes in base64>'; separate entries with ';'.", problem);
    }

    [Fact]
    public void TryParse_RetiredKeyReusingTheCurrentId_FailsNamingTheId()
    {
        var current = TestKeys.Create();

        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(current), TestKeys.RetiredSetting(TestKeys.Create(), TestKeys.Create(current.Id)), out _, out var problem);

        Assert.False(parsed);
        Assert.Equal($"The attachment encryption key id '{current.Id}' appears more than once across EncryptionKey and RetiredEncryptionKeys.", problem);
    }

    [Fact]
    public void TryParse_TwoRetiredKeysSharingAnId_FailsNamingTheId()
    {
        var retired = TestKeys.Create();

        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(TestKeys.Create()), TestKeys.RetiredSetting(retired, TestKeys.Create(retired.Id)), out _, out var problem);

        Assert.False(parsed);
        Assert.Contains($"'{retired.Id}'", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_IdsDifferingOnlyInCase_AreDistinctKeys()
    {
        var current = TestKeys.Create("rotation-a");
        var retired = TestKeys.Create("Rotation-A");

        var parsed = EncryptionKeys.TryParse(TestKeys.Setting(current), TestKeys.Setting(retired), out var keys, out _);

        Assert.True(parsed);
        Assert.Same(keys!.Retired[0], keys.Find("Rotation-A"));
        Assert.Same(keys.Current, keys.Find("rotation-a"));
    }

    [Fact]
    public void TryParse_AnyFailure_NeverQuotesKeyMaterial()
    {
        var current = TestKeys.Create();
        var retired = TestKeys.Create();
        var shortMaterial = Convert.ToBase64String(RandomNumberGenerator.GetBytes(31));
        var failures = new (string? Current, string? Retired)[]
        {
            ($"bad id:{Convert.ToBase64String(current.Material)}", null),
            ($"{current.Id}:{shortMaterial}", null),
            (TestKeys.Setting(current), $"{TestKeys.Setting(retired)};{retired.Id}:{shortMaterial}"),
            (TestKeys.Setting(current), $"{TestKeys.Setting(retired)};{TestKeys.Setting(current)}"),
        };

        foreach (var (currentValue, retiredValue) in failures)
        {
            Assert.False(EncryptionKeys.TryParse(currentValue, retiredValue, out _, out var problem));
            Assert.DoesNotContain(Convert.ToBase64String(current.Material), problem, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToBase64String(retired.Material), problem, StringComparison.Ordinal);
            Assert.DoesNotContain(shortMaterial, problem, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Parse_Valid_ReturnsTheKeySet()
    {
        var current = TestKeys.Create();
        var retired = TestKeys.Create();

        var keys = EncryptionKeys.Parse(TestKeys.Setting(current), TestKeys.Setting(retired));

        Assert.Equal(current.Id, keys.Current.Id);
        Assert.Equal(retired.Material, keys.Find(retired.Id)!.Material);
    }

    [Fact]
    public void Parse_Invalid_ThrowsWithTheProblemAsTheMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EncryptionKeys.Parse(null, null));

        Assert.StartsWith($"{CurrentSetting} is required", exception.Message, StringComparison.Ordinal);
    }
}
