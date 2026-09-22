namespace Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

public sealed class UnitOfWorkSignal
{
    public bool Committed { get; private set; }

    public void MarkCommitted() => Committed = true;
}
