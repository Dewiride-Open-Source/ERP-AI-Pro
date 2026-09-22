namespace Dewiride.Erp.BuildingBlocks.Idempotency.Http;

internal readonly record struct KeyParseResult(KeyParseState State, Guid Key)
{
    public static readonly KeyParseResult Missing = new(KeyParseState.Missing, Guid.Empty);

    public static readonly KeyParseResult Invalid = new(KeyParseState.Invalid, Guid.Empty);

    public static KeyParseResult Valid(Guid key) => new(KeyParseState.Valid, key);
}
