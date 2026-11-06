using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

// A callback runs after the commit, so its failure is logged and the next callback still runs: the change it follows is already
// saved, and neither the caller nor a retry may learn otherwise.
public sealed partial class UnitOfWorkSignal(ILogger<UnitOfWorkSignal> logger)
{
    private List<Action>? _afterCommit;

    public bool Committed { get; private set; }

    public void AfterCommit(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        (_afterCommit ??= []).Add(callback);
    }

    internal void MarkCommitted()
    {
        Committed = true;
        var callbacks = _afterCommit;
        _afterCommit = null;
        foreach (var callback in callbacks ?? [])
        {
            try
            {
                callback();
            }
            catch (Exception exception)
            {
                LogCallbackFailed(exception);
            }
        }
    }

    internal void MarkRolledBack() => _afterCommit = null;

    [LoggerMessage(Level = LogLevel.Error, Message = "A callback after the commit of a unit of work failed; the unit's change stays saved")]
    private partial void LogCallbackFailed(Exception exception);
}
