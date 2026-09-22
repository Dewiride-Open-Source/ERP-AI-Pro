namespace Dewiride.Erp.BuildingBlocks.Idempotency.Http;

internal sealed class RequireIdempotencyKeyMetadata
{
    public static readonly RequireIdempotencyKeyMetadata Instance = new();

    private RequireIdempotencyKeyMetadata()
    {
    }
}
