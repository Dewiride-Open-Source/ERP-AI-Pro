namespace Dewiride.Erp.BuildingBlocks.Jobs.Definitions;

// The delay doubles after each failure and has no jitter: runs are few and claimed one at a time, so retries that fall due
// together overload nothing, and the jobs page can show the exact time of the next attempt.
public sealed record JobRetryPolicy(int MaxAttempts, TimeSpan FirstDelay, TimeSpan MaxDelay)
{
    public const int AttemptsCeiling = 20;

    public static readonly TimeSpan ShortestDelay = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan FirstDelayCeiling = TimeSpan.FromDays(1);

    public static readonly TimeSpan MaxDelayCeiling = TimeSpan.FromDays(7);

    public static JobRetryPolicy Default { get; } = new(5, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

    public TimeSpan DelayAfter(int failures)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failures, 1);

        var doublings = failures - 1;

        return doublings >= 63 || FirstDelay.Ticks > MaxDelay.Ticks >> doublings
            ? MaxDelay
            : TimeSpan.FromTicks(FirstDelay.Ticks << doublings);
    }

    internal void EnsureValid(string jobName)
    {
        if (MaxAttempts is < 1 or > AttemptsCeiling)
        {
            throw new ArgumentException($"The job {jobName} allows {MaxAttempts} attempts; a job allows 1 to {AttemptsCeiling}.");
        }

        if (FirstDelay < ShortestDelay || FirstDelay > FirstDelayCeiling)
        {
            throw new ArgumentException($"The job {jobName} waits {FirstDelay} before its first retry; the wait is {ShortestDelay} to {FirstDelayCeiling}.");
        }

        if (MaxDelay < FirstDelay || MaxDelay > MaxDelayCeiling)
        {
            throw new ArgumentException($"The job {jobName} waits at most {MaxDelay} between attempts; the longest wait is {FirstDelay} to {MaxDelayCeiling}.");
        }
    }
}
