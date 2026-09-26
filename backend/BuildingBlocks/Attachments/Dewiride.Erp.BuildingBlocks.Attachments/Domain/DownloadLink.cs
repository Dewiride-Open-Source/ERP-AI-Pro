using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Domain;

// Only the SHA-256 of the link token is stored, so a copy of the database cannot be turned into working links.
internal sealed class DownloadLink : Entity<DownloadLinkId>
{
    public const int TokenLength = 32;

    public const int TokenHashLength = 32;

    private DownloadLink(DownloadLinkId id)
        : base(id)
    {
    }

    public DownloadLink(DownloadLinkId id, AttachmentId attachmentId, Guid actorId, byte[] tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        ArgumentOutOfRangeException.ThrowIfNotEqual(tokenHash.Length, TokenHashLength, nameof(tokenHash));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, createdAt, nameof(expiresAt));

        AttachmentId = attachmentId;
        ActorId = actorId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public AttachmentId AttachmentId { get; private set; }

    public Guid ActorId { get; private set; }

    public byte[] TokenHash { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
}
