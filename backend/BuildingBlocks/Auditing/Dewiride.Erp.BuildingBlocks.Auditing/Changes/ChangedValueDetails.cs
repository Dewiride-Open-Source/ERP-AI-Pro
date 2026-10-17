using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.BuildingBlocks.Auditing.Changes;

// A value is the text its record holds: a string as it is and any other value as compact JSON, so a list keeps its elements
// apart and a boolean reads "true" or "false". It is null when the field was empty, and on the side a creation or a removal
// has none. Reference names the change key of the type a reference points at, whose names BeforeName and AfterName hold.
public sealed record ChangedValueDetails(
    string Property,
    ChangeValueKind Kind,
    string? Before,
    string? After,
    string? Reference,
    string? BeforeName,
    string? AfterName);
