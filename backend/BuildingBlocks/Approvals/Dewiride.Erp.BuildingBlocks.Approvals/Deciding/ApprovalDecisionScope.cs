namespace Dewiride.Erp.BuildingBlocks.Approvals.Deciding;

// A decision runs the kind's outcome handler, and the domain events of the module's command it calls, inside its own unit of work,
// so a request asked for or withdrawn there would write rows the decision's own save is settling; IApprovals refuses both while
// this scope runs. The mark flows to the code a decision awaits and never back to its caller.
internal static class ApprovalDecisionScope
{
    private static readonly AsyncLocal<bool> Deciding = new();

    public static bool IsRunning => Deciding.Value;

    public static async Task<T> RunAsync<T>(Func<Task<T>> decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        Deciding.Value = true;
        try
        {
            return await decision().ConfigureAwait(false);
        }
        finally
        {
            Deciding.Value = false;
        }
    }
}
