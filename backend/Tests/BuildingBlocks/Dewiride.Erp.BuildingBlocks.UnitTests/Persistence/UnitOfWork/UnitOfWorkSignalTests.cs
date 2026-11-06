using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Persistence.UnitOfWork;

public sealed class UnitOfWorkSignalTests
{
    [Fact]
    public void MarkCommitted_CallbacksRegistered_RunsEachOnceInOrder()
    {
        var signal = new UnitOfWorkSignal(new FakeLogger<UnitOfWorkSignal>());
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
        var signal = new UnitOfWorkSignal(new FakeLogger<UnitOfWorkSignal>());
        var calls = new List<string>();
        signal.AfterCommit(() => calls.Add("rolled back"));
        signal.MarkRolledBack();
        signal.AfterCommit(() => calls.Add("committed"));

        signal.MarkCommitted();

        Assert.Equal(["committed"], calls);
    }

    [Fact]
    public void MarkCommitted_CallbackThrows_LogsItAndRunsTheCallbacksAfterIt()
    {
        var logger = new FakeLogger<UnitOfWorkSignal>();
        var signal = new UnitOfWorkSignal(logger);
        var calls = new List<string>();
        signal.AfterCommit(() => throw new InvalidOperationException("The callback broke."));
        signal.AfterCommit(() => calls.Add("after"));

        signal.MarkCommitted();

        Assert.True(signal.Committed);
        Assert.Equal(["after"], calls);
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.IsType<InvalidOperationException>(record.Exception);
    }

    [Fact]
    public void AfterCommit_NullCallback_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UnitOfWorkSignal(new FakeLogger<UnitOfWorkSignal>()).AfterCommit(null!));
    }
}
