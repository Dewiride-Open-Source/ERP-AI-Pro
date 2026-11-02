using Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Jobs.Running;

public sealed class JobAttemptOutcomeTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(10);

    [Theory]
    [InlineData(ErrorKind.Unavailable, true)]
    [InlineData(ErrorKind.Conflict, true)]
    [InlineData(ErrorKind.Validation, false)]
    [InlineData(ErrorKind.NotFound, false)]
    [InlineData(ErrorKind.Forbidden, false)]
    [InlineData(ErrorKind.Failure, false)]
    [InlineData(ErrorKind.TooLarge, false)]
    [InlineData(ErrorKind.UnsupportedType, false)]
    public void Classify_ErrorKind_RetriesOnlyUnavailableAndConflict(ErrorKind kind, bool retried)
    {
        var error = new Error("test.failure", "The test failed.", kind);

        var outcome = JobAttemptOutcome.Classify(Result.Fail(error), null, leaseLost: false, cancelRequested: false, interrupted: false, overranLimit: null);

        Assert.Equal(new JobAttemptOutcome(retried ? JobAttemptEnd.Retryable : JobAttemptEnd.Permanent, error), outcome);
    }

    [Fact]
    public void Classify_EveryErrorKind_IsInTheTable()
    {
        Assert.Equal(8, Enum.GetValues<ErrorKind>().Length);
    }

    [Fact]
    public void Classify_Success_Succeeds()
    {
        Assert.Equal(JobAttemptEnd.Succeeded, JobAttemptOutcome.Classify(Result.Success(), null, false, false, false, null).End);
    }

    [Fact]
    public void Classify_Exception_RetriesWithTheTypeNameOnly()
    {
        var outcome = JobAttemptOutcome.Classify(null, new InvalidOperationException("Login failed for user 'erp_local'."), false, false, false, null);

        Assert.Equal(JobAttemptEnd.Retryable, outcome.End);
        Assert.Equal(JobAttemptErrors.ExceptionCode, outcome.Error!.Code);
        Assert.Contains(nameof(InvalidOperationException), outcome.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("erp_local", outcome.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_Overran_RetriesAsTimedOut()
    {
        var outcome = JobAttemptOutcome.Classify(null, new OperationCanceledException(), false, false, false, Limit);

        Assert.Equal(JobAttemptEnd.Retryable, outcome.End);
        Assert.Equal(JobAttemptErrors.TimedOutCode, outcome.Error!.Code);
    }

    [Fact]
    public void Classify_Shutdown_IsInterruptedWhateverTheJobAnswered()
    {
        Assert.Equal(JobAttemptEnd.Interrupted, JobAttemptOutcome.Classify(null, new OperationCanceledException(), false, false, true, null).End);
        Assert.Equal(JobAttemptEnd.Interrupted, JobAttemptOutcome.Classify(Result.Fail(Error.Unavailable("test.down", "Down.")), null, false, false, true, Limit).End);
    }

    [Fact]
    public void Classify_CancelRequest_IsCancelledEvenDuringTheShutdown()
    {
        Assert.Equal(JobAttemptEnd.Cancelled, JobAttemptOutcome.Classify(null, new OperationCanceledException(), false, true, true, null).End);
    }

    [Fact]
    public void Classify_SuccessAfterAStopWasAsked_Succeeds()
    {
        Assert.Equal(JobAttemptEnd.Succeeded, JobAttemptOutcome.Classify(Result.Success(), null, false, true, true, Limit).End);
    }

    [Fact]
    public void Classify_LostLease_DiscardsEvenASuccess()
    {
        var outcome = JobAttemptOutcome.Classify(Result.Success(), null, true, false, false, null);

        Assert.Equal(new JobAttemptOutcome(JobAttemptEnd.LeaseLost, null), outcome);
    }
}
