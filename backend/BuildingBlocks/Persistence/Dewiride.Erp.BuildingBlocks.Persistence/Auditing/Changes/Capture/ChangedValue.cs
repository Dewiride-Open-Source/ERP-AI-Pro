namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes.Capture;

internal readonly record struct ChangedValue(RecordedProperty Property, bool HasBefore, object? Before, bool HasAfter, object? After)
{
    public static ChangedValue Added(RecordedProperty property, object? after) => new(property, false, null, true, after);

    public static ChangedValue Removed(RecordedProperty property, object? before) => new(property, true, before, false, null);

    public static ChangedValue Changed(RecordedProperty property, object? before, object? after) => new(property, true, before, true, after);
}
