namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

// A value entry stores its kind as the camelCase member name, decided by the model of the property and never by the value,
// so a reader formats "true", "2026-04-01" or "42" as the property means them.
public enum ChangeValueKind
{
    Text,
    Number,
    Boolean,
    Date,
    Instant,
    Enum,
    List,
    Reference,
    Id,
    Binary,
}
