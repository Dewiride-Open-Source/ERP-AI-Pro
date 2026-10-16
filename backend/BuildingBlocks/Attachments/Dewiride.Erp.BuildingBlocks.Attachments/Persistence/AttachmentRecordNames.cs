using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Persistence;

// A file is named by its file name in the history, after its deletion too, so its name is read past the soft-delete filter.
// The change key is the one its entity configuration declares, read from the model rather than written twice.
internal sealed class AttachmentRecordNames : IRecordNames
{
    private readonly AttachmentsDbContext _context;

    private readonly string _key;

    public AttachmentRecordNames(AttachmentsDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _key = context.Model.FindEntityType(typeof(Attachment)) is { } attachment && RecordedEntityTypes.KeyOf(attachment) is { } key
            ? key
            : throw new InvalidOperationException($"{nameof(AttachmentsDbContext)} maps no change key for {nameof(Attachment)}.");
        EntityTypes = new HashSet<string>(StringComparer.Ordinal) { _key };
    }

    public IReadOnlySet<string> EntityTypes { get; }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (!string.Equals(entityType, _key, StringComparison.Ordinal))
        {
            throw new ArgumentException($"{nameof(AttachmentRecordNames)} names {_key} only, not {entityType}.", nameof(entityType));
        }

        var attachmentIds = ids.Select(AttachmentId.From).ToList();
        var named = await _context.Attachments
            .IncludeDeleted()
            .AsNoTracking()
            .Where(a => attachmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.FileName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return named.ToDictionary(a => a.Id.Value, a => a.FileName);
    }
}
