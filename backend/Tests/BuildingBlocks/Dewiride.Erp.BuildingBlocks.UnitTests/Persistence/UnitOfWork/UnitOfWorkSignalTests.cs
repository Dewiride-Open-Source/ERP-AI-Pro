using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.UnitOfWork;

public sealed class UnitOfWorkSignalTests
{
    [Fact]
    public void MarkCommitted_CallbacksRegistered_RunsEachOnceInOrder()
    {
        var signal = new UnitOfWorkSignal();
        var calls = new List<string>();
        signal.AfterCommit(() => calls.Add("first"));
        signal.AfterCommit(() => calls.Add("second"));

        signal.MarkCommitted();
        signal.MarkCommitted();

        Assert.True(signal.Committed);
        Assert.Equal(["first", "second"], calls);
    }

    [Fact]
    public void MarkCommitted_AfterARollback_RunsOnlyTheCallbacksRegisteredSince()
    {
        var signal = new UnitOfWorkSignal();
        var calls = new List<string>();
        signal.AfterCommit(() => calls.Add("rolled back"));
        signal.MarkRolledBack();
        signal.AfterCommit(() => calls.Add("committed"));

        signal.MarkCommitted();

        Assert.Equal(["committed"], calls);
    }

    [Fact]
    public void AfterCommit_NullCallback_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UnitOfWorkSignal().AfterCommit(null!));
    }
}
