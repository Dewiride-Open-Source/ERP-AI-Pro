using System.Data.Common;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Errors;

public sealed class DatabaseCancellationMiddlewareTests
{
    public static TheoryData<Exception> DatabaseFailures => new()
    {
        new TestDbException(),
        new DbUpdateException("Saving failed.", new TestDbException()),
    };

    [Theory]
    [MemberData(nameof(DatabaseFailures))]
    public async Task InvokeAsync_DatabaseFailureAfterTheRequestWasCancelled_ThrowsCancellationForTheRequestToken(Exception failure)
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var middleware = new DatabaseCancellationMiddleware(_ => throw failure);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task InvokeAsync_DatabaseFailureWhileTheRequestIsLive_RethrowsTheFailure()
    {
        var failure = new TestDbException();
        var middleware = new DatabaseCancellationMiddleware(_ => throw failure);

        var exception = await Assert.ThrowsAsync<TestDbException>(() => middleware.InvokeAsync(new DefaultHttpContext()));

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task InvokeAsync_OtherFailureAfterTheRequestWasCancelled_RethrowsTheFailure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var failure = new InvalidOperationException("Not a database failure.");
        var middleware = new DatabaseCancellationMiddleware(_ => throw failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(new DefaultHttpContext { RequestAborted = cancellation.Token }));

        Assert.Same(failure, exception);
    }

    private sealed class TestDbException : DbException;
}
