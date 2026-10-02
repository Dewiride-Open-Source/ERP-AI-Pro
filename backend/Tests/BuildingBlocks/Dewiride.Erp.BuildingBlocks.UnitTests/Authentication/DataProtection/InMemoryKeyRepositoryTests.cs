using System.Xml.Linq;
using Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.DataProtection;

public sealed class InMemoryKeyRepositoryTests
{
    [Fact]
    public void GetAllElements_BeforeAnyKey_IsEmpty()
    {
        Assert.Empty(new InMemoryKeyRepository().GetAllElements());
    }

    [Fact]
    public void GetAllElements_AfterStoringKeys_ReturnsEachInStoreOrder()
    {
        var repository = new InMemoryKeyRepository();
        var first = Key("first");
        var second = Key("second");

        repository.StoreElement(first, "key-first");
        repository.StoreElement(second, "key-second");

        Assert.Equal([first.ToString(), second.ToString()], repository.GetAllElements().Select(element => element.ToString()));
    }

    [Fact]
    public void StoreElement_ElementChangedAfterwards_KeepsTheStoredCopy()
    {
        var repository = new InMemoryKeyRepository();
        var key = Key("stored");

        repository.StoreElement(key, "key-stored");
        key.SetAttributeValue("id", "changed");

        Assert.Equal("stored", Assert.Single(repository.GetAllElements()).Attribute("id")?.Value);
    }

    [Fact]
    public void GetAllElements_ReturnedElementChanged_LeavesTheRepositoryUnchanged()
    {
        var repository = new InMemoryKeyRepository();
        repository.StoreElement(Key("stored"), "key-stored");

        Assert.Single(repository.GetAllElements()).SetAttributeValue("id", "changed");

        Assert.Equal("stored", Assert.Single(repository.GetAllElements()).Attribute("id")?.Value);
    }

    [Fact]
    public async Task StoreElement_FromManyThreadsAtOnce_KeepsEveryKey()
    {
        var repository = new InMemoryKeyRepository();

        await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() => repository.StoreElement(Key($"key-{index}"), $"key-{index}"), TestContext.Current.CancellationToken)));

        Assert.Equal(32, repository.GetAllElements().Count);
    }

    private static XElement Key(string id) => new("key", new XAttribute("id", id), new XElement("creationDate", "2026-10-02T06:00:00Z"));
}
