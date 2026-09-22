using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Pipeline;

public sealed class PipelineOrderTests
{
    [Fact]
    public async Task HandleAsync_StepsRegisteredInReverse_RunLoggingThenValidationThenUnitOfWork()
    {
        await using var provider = Build(PipelineStage.UnitOfWork, PipelineStage.Validation, PipelineStage.Logging);
        await using var scope = provider.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<EchoCommand, string>>();
        var result = await handler.HandleAsync(new EchoCommand("x"), TestContext.Current.CancellationToken);

        Assert.Equal("x", result.Value);
        Assert.Equal(["enter Logging", "enter Validation", "enter UnitOfWork", "handler", "leave UnitOfWork", "leave Validation", "leave Logging"], scope.ServiceProvider.GetRequiredService<Trace>().Entries);
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_IsRejectedBeforeTheUnitOfWorkAndTheHandler()
    {
        await using var provider = Build(PipelineStage.UnitOfWork);
        await using var scope = provider.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<EchoCommand, string>>();
        var result = await handler.HandleAsync(new EchoCommand(null!), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("request.invalid", result.Error!.Code);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal(["The Text field is required."], result.Error.Fields[nameof(EchoCommand.Text)]);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<Trace>().Entries);
    }

    [Fact]
    public async Task HandleAsync_QueryImplementingIValidatableObject_ReportsItsOwnFailures()
    {
        await using var provider = Build();
        await using var scope = provider.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<RangeQuery, int>>();
        var result = await handler.HandleAsync(new RangeQuery(5, 1), TestContext.Current.CancellationToken);

        Assert.Equal("request.invalid", result.Error!.Code);
        Assert.Equal(["'To' must not be before 'From'."], result.Error.Fields[nameof(RangeQuery.To)]);
    }

    [Fact]
    public async Task HandleAsync_ThrowingHandler_PropagatesTheExceptionThroughEveryStep()
    {
        await using var provider = Build(PipelineStage.UnitOfWork);
        await using var scope = provider.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<EchoCommand, string>>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new EchoCommand("throw"), TestContext.Current.CancellationToken));
        Assert.Equal(["enter UnitOfWork", "handler"], scope.ServiceProvider.GetRequiredService<Trace>().Entries);
    }

    private static ServiceProvider Build(params PipelineStage[] recordingStages)
    {
        var services = new ServiceCollection().AddLogging().AddScoped<Trace>();
        foreach (var stage in recordingStages)
        {
            services.AddScoped<IPipelineStep>(provider => new RecordingStep(stage, provider.GetRequiredService<Trace>()));
        }

        services.AddHandlersFromAssembly(typeof(PipelineOrderTests).Assembly);

        return services.BuildServiceProvider();
    }

    internal sealed record EchoCommand([property: Required] string Text) : ICommand<string>;

    internal sealed record RangeQuery(int From, int To) : IQuery<int>, IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (To < From)
            {
                yield return new ValidationResult("'To' must not be before 'From'.", [nameof(To)]);
            }
        }
    }

    internal sealed class Trace
    {
        public List<string> Entries { get; } = [];
    }

    internal sealed class EchoHandler(Trace trace) : ICommandHandler<EchoCommand, string>
    {
        public Task<Result<string>> HandleAsync(EchoCommand command, CancellationToken cancellationToken)
        {
            trace.Entries.Add("handler");

            return command.Text == "throw"
                ? throw new InvalidOperationException("boom")
                : Task.FromResult(Result.Success(command.Text));
        }
    }

    internal sealed class RangeHandler : IQueryHandler<RangeQuery, int>
    {
        public Task<Result<int>> HandleAsync(RangeQuery query, CancellationToken cancellationToken) => Task.FromResult(Result.Success(query.To - query.From));
    }

    private sealed class RecordingStep(PipelineStage stage, Trace trace) : IPipelineStep
    {
        public PipelineStage Stage => stage;

        public async Task<Result<TResult>> InvokeAsync<TRequest, TResult>(TRequest request, HandlerDescriptor descriptor, PipelineContinuation<TResult> next, CancellationToken cancellationToken)
            where TRequest : notnull
        {
            trace.Entries.Add($"enter {stage}");
            var result = await next(cancellationToken);
            trace.Entries.Add($"leave {stage}");

            return result;
        }
    }
}
