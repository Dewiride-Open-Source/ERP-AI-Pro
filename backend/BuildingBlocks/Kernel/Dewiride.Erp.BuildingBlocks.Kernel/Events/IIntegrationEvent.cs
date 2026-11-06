namespace Dewiride.Erp.BuildingBlocks.Kernel.Events;

// What one module tells the others, delivered at least once and in no particular order. Its name is its identity in the outbox
// and in every delivery, so it never changes: three lower-case kebab segments, `<domain>.<module>.<event>`, the first two the
// declaring module's permission prefix.
public interface IIntegrationEvent
{
    static abstract string Name { get; }
}
