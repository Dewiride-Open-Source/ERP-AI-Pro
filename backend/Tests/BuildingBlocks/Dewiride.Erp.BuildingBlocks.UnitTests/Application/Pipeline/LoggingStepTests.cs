using System.Diagnostics;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline.Steps;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Pipeline;

public sealed class LoggingStepTests
{
    private static readonly HandlerDescriptor Command = new(typeof(LoggingStepTests), typeof(SampleCommand), HandlerKind.Command);

    private static readonly HandlerDescriptor Query = new(typeof(LoggingStepTests), typeof(SampleQuery), HandlerKind.Query);

    [Fact]
    public async Task InvokeAsync_SuccessfulCommand_LogsHandledAtInformation()
    {
        var logger = new FakeLogger<LoggingStep>();

        var result = await new LoggingStep(logger).InvokeAsync(new SampleCommand(), Command, _ => Task.FromResult(Result.Success(1)), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Value);
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.StartsWith("Handled SampleCommand in ", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulQuery_LogsHandledAtDebug()
    {
        var logger = new FakeLogger<LoggingStep>();

        await new LoggingStep(logger).InvokeAsync(new SampleQuery(), Query, _ => Task.FromResult(Result.Success(1)), TestContext.Current.CancellationToken);

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Collector.GetSnapshot()).Level);
    }

    [Fact]
    public async Task InvokeAsync_FailedCommand_LogsTheErrorCodeAndReturnsTheFailure()
    {
        var logger = new FakeLogger<LoggingStep>();

        var result = await new LoggingStep(logger).InvokeAsync(new SampleCommand(), Command, _ => Task.FromResult(Result.Fail<int>(Error.NotFound("sample.missing", "gone"))), TestContext.Current.CancellationToken);

        Assert.Equal("sample.missing", result.Error!.Code);
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.StartsWith("Rejected SampleCommand with sample.missing in ", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_ThrowingHandler_LogsAWarningAndRethrows()
    {
        var logger = new FakeLogger<LoggingStep>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new LoggingStep(logger).InvokeAsync<SampleCommand, int>(new SampleCommand(), Command, _ => throw new InvalidOperationException("boom"), TestContext.Current.CancellationToken));

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.StartsWith("SampleCommand threw InvalidOperationException after ", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_Cancellation_IsNotLogged()
    {
        var logger = new FakeLogger<LoggingStep>();

        await Assert.ThrowsAsync<OperationCanceledException>(() => new LoggingStep(logger).InvokeAsync<SampleCommand, int>(new SampleCommand(), Command, _ => throw new OperationCanceledException(), TestContext.Current.CancellationToken));

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task InvokeAsync_WithAListener_StartsAnActivityNamedAfterTheRequestCarryingHandlerTags()
    {
        Activity? started = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == LoggingStep.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => started = activity,
        };
        ActivitySource.AddActivityListener(listener);

        await new LoggingStep(new FakeLogger<LoggingStep>()).InvokeAsync(new SampleCommand(), Command, _ => Task.FromResult(Result.Fail<int>(Error.Conflict("sample.busy", "busy"))), TestContext.Current.CancellationToken);

        Assert.NotNull(started);
        Assert.Equal(nameof(SampleCommand), started.OperationName);
        Assert.Equal(typeof(SampleCommand).FullName, started.GetTagItem("erp.request"));
        Assert.Equal(typeof(LoggingStepTests).FullName, started.GetTagItem("erp.handler"));
        Assert.Equal("Command", started.GetTagItem("erp.handler.kind"));
        Assert.Equal("sample.busy", started.GetTagItem("erp.error.code"));
        Assert.Equal(ActivityStatusCode.Error, started.Status);
    }

    private sealed record SampleCommand;

    private sealed record SampleQuery;
}
