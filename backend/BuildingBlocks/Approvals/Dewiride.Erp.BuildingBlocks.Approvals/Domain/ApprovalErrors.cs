using Dewiride.Erp.BuildingBlocks.Approvals.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Domain;

public static class ApprovalErrors
{
    public const string CommentField = "Comment";

    public const string VersionField = "Version";

    public const string KindField = "Kind";

    public const string WorkingDaysField = "WorkingDaysToDecide";

    public const string AllRequestsForbiddenCode = "request.forbidden";

    public static readonly Error RequestNotFound = Error.NotFound("approval.request-not-found", "No approval request has this id.");

    public static readonly Error NotWaiting = Error.Conflict("approval.not-waiting", "The request no longer waits for a decision.");

    public static readonly Error OwnRequest = Error.Forbidden("approval.own-request", "Nobody decides a request they asked for or submitted.");

    public static readonly Error NotYours = Error.Forbidden("approval.not-yours", "The request does not wait for your decision.");

    public static readonly Error NotYoursToWithdraw = Error.Forbidden("approval.not-yours-to-withdraw", "Only the person the request is for, or the person who submitted it, may withdraw it.");

    public static readonly Error AllRequestsForbidden = Error.Forbidden(AllRequestsForbiddenCode, "Every approval request is shown only to a person who may see them all.");

    public static readonly Error Busy = Error.Conflict(EfUnitOfWork.ConcurrencyConflictCode, "Another decision on this request is being saved; try again.");

    public static readonly Error Stale = Error.Conflict(EfUnitOfWork.ConcurrencyConflictCode, "The request changed since it was read; reload it and try again.");

    public static readonly Error VersionInvalid = Error.Validation("approval.version-invalid", "The version is not one the API gave.", Field(VersionField, "Send the version the API last returned."));

    public static readonly Error CommentTooLong = Error.Validation("approval.comment-too-long", "The comment is too long.", Field(CommentField, $"Use at most {ApprovalStep.CommentMaxLength} characters."));

    public static readonly Error SettingNotFound = Error.NotFound("approval.setting-not-found", "No approval setting has this id.");

    public static readonly Error WorkingDaysOutOfRange =
        Error.Validation("approval.working-days-out-of-range", "The working days are outside the days a step may have.", Field(WorkingDaysField, $"Give each step {ApprovalKind.MinWorkingDays} to {ApprovalKind.MaxWorkingDays} working days."));

    public static Error KindRetired(string kind) =>
        Error.Failure("approval.kind-retired", $"This release no longer asks approval for {kind}, so its requests can be neither decided nor withdrawn here.");

    public static Error AlreadySubmitted(ApprovalKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return Error.Conflict("approval.already-submitted", $"This record already waits for {kind.Title}; decide or withdraw that request first.");
    }

    public static Error ReasonRequired(ApprovalState closing) =>
        Error.Validation(
            "approval.reason-required",
            closing == ApprovalState.Returned ? "A request is sent back with what needs to change." : "A request is rejected with the reason why.",
            Field(CommentField, closing == ApprovalState.Returned ? "Say what needs to change." : "Say why it is rejected."));

    public static Error UnknownKind(string kind) =>
        Error.Validation("approval.kind-unknown", $"No kind of request is approved under the key {kind}.", Field(KindField, "Choose a kind of request the ERP asks approval for."));

    public static Error SettingExists(ApprovalKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return Error.Conflict("approval.setting-exists", $"{kind.Title} already has a setting; change it instead.");
    }

    private static Dictionary<string, string[]> Field(string field, string message) => new(StringComparer.Ordinal) { [field] = [message] };
}
