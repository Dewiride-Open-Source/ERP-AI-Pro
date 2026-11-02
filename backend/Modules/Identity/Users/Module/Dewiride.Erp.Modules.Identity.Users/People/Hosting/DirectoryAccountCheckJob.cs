using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.People.Application;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.Modules.Identity.Users.People.Hosting;

// The check an administrator runs from People, run on its schedule as the system. The runner builds the job in each attempt's
// scope, so it can take the directory, which only the API composes. The system has no account for the check's own-account guard
// to find, so only the guard against an answer without an enabled account stands between a wrong answer and the deactivations
// (ADR-0059). A second run at once changes nothing: the check takes the organisation lock and reads only active records. The
// retries wait 5, 10, 20, 40 and 60 minutes, so from an occurrence at 00:30 India time a run whose attempts fail at once gives up
// at 02:45, before the server's 03:00 update restart.
internal sealed partial class DirectoryAccountCheckJob(
    DirectoryAccountCheck check,
    IDirectoryAccounts directory,
    ILogger<DirectoryAccountCheckJob> logger) : IJob
{
    public static JobDefinition Definition { get; } = new(
        "identity.users.directory-account-check",
        "Check Microsoft accounts",
        "Asks Microsoft about everyone in People who is linked to a Microsoft account and deactivates each person whose account is disabled or deleted, so any page they have open ends at their next click.",
        new JobRetryPolicy(6, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1)),
        TimeSpan.FromMinutes(10));

    public async Task<Result> RunAsync(JobContext context, CancellationToken cancellationToken)
    {
        var result = await check.RunAsync(directory, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        LogChecked(logger, result.Value.CheckedCount, result.Value.Deactivated.Count);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The nightly account check read {CheckedCount} accounts and deactivated {DeactivatedCount} people")]
    private static partial void LogChecked(ILogger logger, int checkedCount, int deactivatedCount);
}
