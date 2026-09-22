using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.DependencyInjection;

public sealed class HandlerRegistrationTests
{
    [Fact]
    public void AddHandlersFromAssembly_RegistersEveryHandlerAsItselfAndItsInterfaceThroughAFactory()
    {
        var services = new ServiceCollection();

        services.AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);

        var command = Assert.Single(services, d => d.ServiceType == typeof(ICommandHandler<PingCommand, string>));
        var query = Assert.Single(services, d => d.ServiceType == typeof(IQueryHandler<CountQuery, int>));
        Assert.NotNull(command.ImplementationFactory);
        Assert.NotNull(query.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Scoped, command.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, query.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, d => d.ServiceType == typeof(PingHandler)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, d => d.ServiceType == typeof(CountHandler)).Lifetime);
    }

    [Fact]
    public void AddHandlersFromAssembly_RegistersDomainEventHandlersAndThePipelineSteps()
    {
        var services = new ServiceCollection();

        services.AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);

        var eventHandler = Assert.Single(services, d => d.ServiceType == typeof(IDomainEventHandler<PingedEvent>));
        Assert.Equal(typeof(PingedEventHandler), eventHandler.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, eventHandler.Lifetime);
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IPipelineStep)));
        Assert.Single(services, d => d.ServiceType == typeof(DomainEventDispatcher));
    }

    [Fact]
    public void AddHandlersFromAssembly_CalledTwice_RegistersThePipelineStepsOnce()
    {
        var services = new ServiceCollection();

        services.AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);
        services.AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);

        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IPipelineStep)));
        Assert.Single(services, d => d.ServiceType == typeof(PingHandler));
    }

    [Fact]
    public void AddHandlersFromAssembly_SkipsAbstractHandlers()
    {
        var services = new ServiceCollection();

        services.AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);

        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(AbstractPingHandler));
    }

    [Fact]
    public async Task ResolvedHandler_IsThePipelineWrappingTheConcreteHandler()
    {
        var services = new ServiceCollection().AddLogging().AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, string>>();
        var result = await handler.HandleAsync(new PingCommand("hi"), TestContext.Current.CancellationToken);

        Assert.Equal("pong: hi", result.Value);
        Assert.Equal("CommandHandlerPipeline`2", handler.GetType().Name);
        Assert.IsType<PingHandler>(scope.ServiceProvider.GetRequiredService<PingHandler>());
    }

    internal sealed record PingCommand(string Text) : ICommand<string>;

    internal sealed record CountQuery : IQuery<int>;

    internal sealed record PingedEvent(DateTimeOffset OccurredOn) : IDomainEvent;

    internal sealed class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<Result<string>> HandleAsync(PingCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success($"pong: {command.Text}"));
    }

    internal sealed class CountHandler : IQueryHandler<CountQuery, int>
    {
        public Task<Result<int>> HandleAsync(CountQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(1));
    }

    internal sealed class PingedEventHandler : IDomainEventHandler<PingedEvent>
    {
        public Task HandleAsync(PingedEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    internal abstract class AbstractPingHandler : ICommandHandler<PingCommand, string>
    {
        public abstract Task<Result<string>> HandleAsync(PingCommand command, CancellationToken cancellationToken);
    }
}
