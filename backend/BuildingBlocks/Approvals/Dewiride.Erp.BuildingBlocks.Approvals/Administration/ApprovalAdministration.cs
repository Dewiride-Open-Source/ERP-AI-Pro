using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Administration;

// Each change runs in a unit of work of its own on the approvals' own context. A setting is read before it changes, so the version
// the change was made from becomes the original rowversion, and every change writes the setting's row, even one that changes
// nothing, so its version moves with every change and saving refuses one made from an older version. Two set-ups of one kind at
// once leave one: the unique index of the kind refuses the other.
internal sealed class ApprovalAdministration(ApprovalsDbContext context, ApprovalKindCatalog catalog, DomainEventDispatcher dispatcher, UnitOfWorkSignal signal) : IApprovalAdministration
{
    public async Task<IReadOnlyList<ApprovalKindDetails>> ListAsync(CancellationToken cancellationToken)
    {
        var settings = await context.KindSettings.AsNoTracking().ToDictionaryAsync(s => s.Kind, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);

        return [.. catalog.Kinds.Select(registration => new ApprovalKindDetails(registration.Kind, settings.TryGetValue(registration.Kind.Key, out var setting) ? DetailsOf(setting) : null))];
    }

    public async Task<Result<ApprovalKindDetails>> SetUpAsync(string kind, bool required, int workingDaysToDecide, CancellationToken cancellationToken)
    {
        var registration = catalog.Find(kind);
        if (registration is null)
        {
            return ApprovalErrors.UnknownKind(kind);
        }

        if (workingDaysToDecide is < ApprovalKind.MinWorkingDays or > ApprovalKind.MaxWorkingDays)
        {
            return ApprovalErrors.WorkingDaysOutOfRange;
        }

        var saved = await EfUnitOfWork.RunAsync<ApprovalKindSettingId>(
            context,
            dispatcher,
            signal,
            async token =>
            {
                if (await context.KindSettings.AnyAsync(s => s.Kind == kind, token).ConfigureAwait(false))
                {
                    return ApprovalErrors.SettingExists(registration.Kind);
                }

                var setting = ApprovalKindSetting.Create(kind, required, workingDaysToDecide);
                context.KindSettings.Add(setting);

                return setting.Id;
            },
            cancellationToken).ConfigureAwait(false);

        return saved.IsFailure ? saved.Error! : await DetailsOfAsync(registration, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ApprovalKindDetails>> ChangeAsync(ApprovalKindSettingId id, byte[] version, bool required, int workingDaysToDecide, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (workingDaysToDecide is < ApprovalKind.MinWorkingDays or > ApprovalKind.MaxWorkingDays)
        {
            return ApprovalErrors.WorkingDaysOutOfRange;
        }

        ApprovalKindRegistration? changed = null;
        var saved = await EfUnitOfWork.RunAsync<ApprovalKindSettingId>(
            context,
            dispatcher,
            signal,
            async token =>
            {
                var setting = await context.KindSettings.SingleOrDefaultAsync(s => s.Id == id, token).ConfigureAwait(false);
                var registration = setting is null ? null : catalog.Find(setting.Kind);
                if (setting is null || registration is null)
                {
                    return ApprovalErrors.SettingNotFound;
                }

                changed = registration;
                context.Entry(setting).Property(s => s.RowVersion).OriginalValue = version;
                context.Entry(setting).State = EntityState.Modified;
                setting.Change(required, workingDaysToDecide);

                return setting.Id;
            },
            cancellationToken).ConfigureAwait(false);

        return saved.IsFailure ? saved.Error! : await DetailsOfAsync(changed!, cancellationToken).ConfigureAwait(false);
    }

    private static ApprovalKindSettingDetails DetailsOf(ApprovalKindSetting setting) =>
        new(setting.Id, setting.Required, setting.WorkingDaysToDecide, RecordVersion.Encode(setting.RowVersion));

    private async Task<ApprovalKindDetails> DetailsOfAsync(ApprovalKindRegistration registration, CancellationToken cancellationToken)
    {
        var setting = await context.KindSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Kind == registration.Kind.Key, cancellationToken).ConfigureAwait(false);

        return new ApprovalKindDetails(registration.Kind, setting is null ? null : DetailsOf(setting));
    }
}
