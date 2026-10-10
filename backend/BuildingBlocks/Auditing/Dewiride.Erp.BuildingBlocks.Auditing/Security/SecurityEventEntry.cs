namespace Dewiride.Erp.BuildingBlocks.Auditing.Security;

// A record names a person only by their Microsoft Entra object id and a caller only by its client address and its client
// application id, never by a name, an email or a token: an investigation needs no more, and a record holds only what it needs.
public readonly record struct SecurityEventEntry(
    SecurityEventKind Kind,
    string? Detail = null,
    Guid? ActorObjectId = null,
    Guid? ClientApplicationId = null,
    string? ClientAddress = null,
    string? CorrelationId = null);
