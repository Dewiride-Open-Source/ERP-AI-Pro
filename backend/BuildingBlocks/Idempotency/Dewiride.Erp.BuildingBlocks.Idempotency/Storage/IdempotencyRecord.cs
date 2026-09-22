namespace Dewiride.Erp.BuildingBlocks.Idempotency.Storage;

internal sealed class IdempotencyRecord
{
    public const int FingerprintLength = 32;

    public const int ContentTypeMaxLength = 256;

    public const int LocationMaxLength = 2048;

    private IdempotencyRecord()
    {
    }

    public Guid ActorId { get; private set; }

    public Guid Key { get; private set; }

    public byte[] Fingerprint { get; private set; } = [];

    public IdempotencyStatus Status { get; private set; }

    public int? StatusCode { get; private set; }

    public string? ContentType { get; private set; }

    public string? Location { get; private set; }

    public byte[]? Body { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static IdempotencyRecord Start(Guid actorId, Guid key, byte[] fingerprint, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        if (fingerprint.Length != FingerprintLength)
        {
            throw new ArgumentException($"A fingerprint is {FingerprintLength} bytes.", nameof(fingerprint));
        }

        return new IdempotencyRecord
        {
            ActorId = actorId,
            Key = key,
            Fingerprint = fingerprint,
            Status = IdempotencyStatus.InProgress,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
    }

    public bool Matches(byte[] fingerprint) => Fingerprint.AsSpan().SequenceEqual(fingerprint);
}
