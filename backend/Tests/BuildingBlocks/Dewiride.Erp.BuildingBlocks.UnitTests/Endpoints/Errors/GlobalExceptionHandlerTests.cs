using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Errors;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_BodyOverTheLimit_AnswersRequestTooLargeAtWarning()
    {
        var (problem, record, context) = await HandleAsync(new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, problem.Status);
        Assert.Equal(ProblemTypes.RequestTooLarge, problem.Extensions[ResultExtensions.CodeExtension]);
        Assert.Null(problem.Title);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Null(record.Exception);
    }

    [Fact]
    public async Task TryHandleAsync_ValueTheBinderCannotRead_AnswersRequestMalformedWithItsStatusAtWarning()
    {
        var (problem, record, context) = await HandleAsync(new BadHttpRequestException("Failed to bind parameter \"Nullable<int> Take\" from \"many\".", StatusCodes.Status400BadRequest));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(ProblemTypes.RequestMalformed, problem.Extensions[ResultExtensions.CodeExtension]);
        Assert.Equal("The request could not be read.", problem.Title);
        Assert.Equal(LogLevel.Warning, record.Level);
    }

    [Fact]
    public async Task TryHandleAsync_CancellationThatReachesTheHandler_AnswersServerErrorAtError()
    {
        var (problem, record, context) = await HandleAsync(new OperationCanceledException());

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(ProblemTypes.ServerError, problem.Extensions[ResultExtensions.CodeExtension]);
        Assert.Equal(LogLevel.Error, record.Level);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_AnswersServerErrorWithoutItsMessageAndLogsIt()
    {
        var failure = new InvalidOperationException("connection string Password=secret");

        var (problem, record, context) = await HandleAsync(failure);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("An unexpected error occurred.", problem.Title);
        Assert.Null(problem.Detail);
        Assert.Equal("/api/finance/sales/invoices", problem.Instance);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Same(failure, record.Exception);
    }

    private static async Task<(ProblemDetails Problem, FakeLogRecord Record, HttpContext Context)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/finance/sales/invoices";
        var problems = new RecordingProblemDetailsService();
        var logger = new FakeLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(problems, logger);

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        return (Assert.IsType<ProblemDetails>(problems.Written), Assert.Single(logger.Collector.GetSnapshot()), context);
    }

    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }
    }
}
