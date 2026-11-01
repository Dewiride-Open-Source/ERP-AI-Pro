using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Domain;

// What a run keeps of an error: the code when it is short printable ASCII, and the message cut to fit. Both are written by code
// for an administrator and name no person, value or secret.
internal readonly record struct StoredError(string Code, string Message)
{
    public const string UnnamedCode = "job.failed";

    public static StoredError From(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var code = error.Code is { Length: > 0 and <= JobRun.ErrorCodeMaxLength } candidate && candidate.All(character => character is > ' ' and <= '~')
            ? error.Code
            : UnnamedCode;
        var message = error.Message.Length > JobRun.ErrorMessageMaxLength ? error.Message[..JobRun.ErrorMessageMaxLength] : error.Message;

        return new StoredError(code, message);
    }
}
