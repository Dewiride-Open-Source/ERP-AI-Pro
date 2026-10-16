namespace Dewiride.Erp.BuildingBlocks.Kernel.Domain;

// A row that records something that happened: it is inserted once and never changed, and only the retention of its table
// deletes it, outside the change tracker, so the change tracker refuses to update or delete one.
public interface IAppendOnly
{
}
