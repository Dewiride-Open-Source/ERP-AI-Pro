namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

// Stored as its number, so a value is never given to another operation.
public enum ChangeOperation : byte
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    Restored = 4,
}
