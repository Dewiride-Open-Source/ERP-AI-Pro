namespace Dewiride.Erp.BuildingBlocks.Caching.Persistence;

// The table belongs to SqlServerCache, which reads and writes it with its own SQL; this type only gives the migration its
// columns, so no code creates or queries an entry.
internal sealed class DistributedCacheEntry
{
    private DistributedCacheEntry()
    {
    }

    public string Id { get; private set; } = string.Empty;

    public byte[] Value { get; private set; } = [];

    public DateTimeOffset ExpiresAtTime { get; private set; }

    public long? SlidingExpirationInSeconds { get; private set; }

    public DateTimeOffset? AbsoluteExpiration { get; private set; }
}
