using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Persistence;

// A request is named by its kind, as this release titles it, and the day in India it was submitted, a step by its title and its
// request's kind, and a setting by its kind; a kind no release declares is named by its key. The change keys are the ones the
// entity configurations declare, read from the model rather than written twice.
internal sealed class ApprovalRecordNames : IRecordNames
{
    private readonly ApprovalsDbContext _context;

    private readonly ApprovalKindCatalog _catalog;

    private readonly string _requests;

    private readonly string _steps;

    private readonly string _settings;

    public ApprovalRecordNames(ApprovalsDbContext context, ApprovalKindCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        _context = context;
        _catalog = catalog;
        _requests = KeyOf<ApprovalRequest>(context);
        _steps = KeyOf<ApprovalStep>(context);
        _settings = KeyOf<ApprovalKindSetting>(context);
        EntityTypes = new HashSet<string>(StringComparer.Ordinal) { _requests, _steps, _settings };
    }

    public IReadOnlySet<string> EntityTypes { get; }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (string.Equals(entityType, _requests, StringComparison.Ordinal))
        {
            var requestIds = ids.Select(ApprovalRequestId.From).ToList();
            var requests = await _context.Requests
                .AsNoTracking()
                .Where(r => requestIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Kind, r.SubmittedAt })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return requests.ToDictionary(r => r.Id.Value, r => string.Create(CultureInfo.InvariantCulture, $"{_catalog.TitleOf(r.Kind)}, {IndianStandardTime.IstDate(r.SubmittedAt):dd-MM-yyyy}"));
        }

        if (string.Equals(entityType, _steps, StringComparison.Ordinal))
        {
            var stepIds = ids.Select(ApprovalStepId.From).ToList();
            var steps = await _context.Steps
                .AsNoTracking()
                .Where(s => stepIds.Contains(s.Id))
                .Join(_context.Requests.AsNoTracking(), s => s.RequestId, r => r.Id, (s, r) => new { s.Id, s.Title, r.Kind })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return steps.ToDictionary(s => s.Id.Value, s => $"{s.Title} of {_catalog.TitleOf(s.Kind)}");
        }

        if (string.Equals(entityType, _settings, StringComparison.Ordinal))
        {
            var settingIds = ids.Select(ApprovalKindSettingId.From).ToList();
            var settings = await _context.KindSettings
                .AsNoTracking()
                .Where(s => settingIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Kind })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return settings.ToDictionary(s => s.Id.Value, s => _catalog.TitleOf(s.Kind));
        }

        throw new ArgumentException($"{nameof(ApprovalRecordNames)} names {_requests}, {_steps} and {_settings} only, not {entityType}.", nameof(entityType));
    }

    private static string KeyOf<TEntity>(ApprovalsDbContext context) =>
        context.Model.FindEntityType(typeof(TEntity)) is { } entityType && RecordedEntityTypes.KeyOf(entityType) is { } key
            ? key
            : throw new InvalidOperationException($"{nameof(ApprovalsDbContext)} maps no change key for {typeof(TEntity).Name}.");
}
