namespace Dewiride.Erp.BuildingBlocks.Idempotency.Storage;

internal sealed record StoredResponse(int StatusCode, string? ContentType, string? Location, byte[]? Body);
