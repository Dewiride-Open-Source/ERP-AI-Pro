using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Encryption;
using Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Fakes;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Attachments.Encryption;

public sealed class KeyRingTests
{
    [Fact]
    public void Current_ConfiguredKey_ReturnsItsIdAndMaterial()
    {
        var current = TestKeys.Create();
        var ring = new KeyRing(Monitor(current));

        Assert.Equal(current.Id, ring.Current.Id);
        Assert.Equal(current.Material, ring.Current.Material);
    }

    [Fact]
    public void Find_CurrentOrRetiredId_ReturnsThatKey()
    {
        var current = TestKeys.Create();
        var first = TestKeys.Create();
        var second = TestKeys.Create();
        var ring = new KeyRing(Monitor(current, first, second));

        Assert.Equal(current.Material, ring.Find(current.Id)!.Material);
        Assert.Equal(first.Material, ring.Find(first.Id)!.Material);
        Assert.Equal(second.Material, ring.Find(second.Id)!.Material);
    }

    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        var current = TestKeys.Create();
        var ring = new KeyRing(Monitor(current, TestKeys.Create()));

        Assert.Null(ring.Find(TestKeys.NewId()));
        Assert.Null(ring.Find(current.Id.ToUpperInvariant()));
    }

    [Fact]
    public void All_CurrentAndRetiredKeys_ListsTheCurrentKeyFirst()
    {
        var current = TestKeys.Create();
        var retired = TestKeys.Create();
        var ring = new KeyRing(Monitor(current, retired));

        Assert.Equal([current.Id, retired.Id], ring.All.Select(key => key.Id));
    }

    [Fact]
    public void Current_UnchangedOptions_ReusesTheParsedKey()
    {
        var ring = new KeyRing(Monitor(TestKeys.Create()));

        Assert.Same(ring.Current, ring.Current);
    }

    [Fact]
    public void Current_AfterARotation_ReturnsTheNewKeyAndStillFindsTheRetiredOne()
    {
        var original = TestKeys.Create();
        var rotated = TestKeys.Create();
        var monitor = Monitor(original);
        var ring = new KeyRing(monitor);
        Assert.Equal(original.Id, ring.Current.Id);

        monitor.CurrentValue = Options(rotated, original);

        Assert.Equal(rotated.Id, ring.Current.Id);
        Assert.Equal(rotated.Material, ring.Current.Material);
        Assert.Equal(original.Material, ring.Find(original.Id)!.Material);
    }

    [Fact]
    public void Find_AfterARetiredKeyIsDropped_ReturnsNull()
    {
        var current = TestKeys.Create();
        var retired = TestKeys.Create();
        var monitor = Monitor(current, retired);
        var ring = new KeyRing(monitor);
        Assert.NotNull(ring.Find(retired.Id));

        monitor.CurrentValue = Options(current);

        Assert.Null(ring.Find(retired.Id));
    }

    [Fact]
    public void Current_InvalidOptions_ThrowsTheParseProblem()
    {
        var ring = new KeyRing(new SettableOptionsMonitor<AttachmentsOptions>(new AttachmentsOptions()));

        var exception = Assert.Throws<InvalidOperationException>(() => ring.Current);

        Assert.StartsWith($"{AttachmentsOptions.SectionName}:EncryptionKey is required", exception.Message, StringComparison.Ordinal);
    }

    private static SettableOptionsMonitor<AttachmentsOptions> Monitor(EncryptionKey current, params EncryptionKey[] retired) => new(Options(current, retired));

    private static AttachmentsOptions Options(EncryptionKey current, params EncryptionKey[] retired) =>
        new() { EncryptionKey = TestKeys.Setting(current), RetiredEncryptionKeys = TestKeys.RetiredSetting(retired) };
}
