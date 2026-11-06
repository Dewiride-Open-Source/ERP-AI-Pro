namespace Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

public sealed class UnitOfWorkSignal
{
    private List<Action>? _afterCommit;

    public bool Committed { get; private set; }

    public void AfterCommit(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        (_afterCommit ??= []).Add(callback);
    }

    public void MarkCommitted()
    {
        Committed = true;
        var callbacks = _afterCommit;
        _afterCommit = null;
        foreach (var callback in callbacks ?? [])
        {
            callback();
        }
    }

    public void MarkRolledBack() => _afterCommit = null;
}
