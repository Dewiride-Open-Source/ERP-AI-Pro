namespace Dewiride.Erp.BuildingBlocks.Kernel.Domain;

public interface IVersioned
{
    byte[] RowVersion { get; }
}
