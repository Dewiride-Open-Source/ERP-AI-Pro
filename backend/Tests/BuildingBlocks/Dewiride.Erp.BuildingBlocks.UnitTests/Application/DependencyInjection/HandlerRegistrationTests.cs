using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.DependencyInjection;

public sealed class HandlerRegistrationTests
{
    [Fact]
    public void AddHandlersFromAssembly_RegistersConcreteCommandAndQueryHandlersAsScoped()
    {
        var services = new ServiceCollection();

        services.AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);

        var command = Assert.Single(services, d => d.ServiceType == typeof(ICommandHandler<PingCommand, string>));
        var query = Assert.Single(services, d => d.ServiceType == typeof(IQueryHandler<CountQuery, int>));
        Assert.Equal(typeof(PingHandler), command.ImplementationType);
        Assert.Equal(typeof(CountHandler), query.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, command.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, query.Lifetime);
    }

    [Fact]
    public void AddHandlersFromAssembly_SkipsAbstractHandlers()
    {
        var services = new ServiceCollection();

        services.AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);

        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(AbstractPingHandler));
    }

    [Fact]
    public async Task ResolvedHandler_Executes()
    {
        var services = new ServiceCollection().AddHandlersFromAssembly(typeof(HandlerRegistrationTests).Assembly);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, string>>();
        var result = await handler.HandleAsync(new PingCommand("hi"), TestContext.Current.CancellationToken);

        Assert.Equal("pong: hi", result.Value);
    }

    internal sealed record PingCommand(string Text) : ICommand<string>;

    internal sealed record CountQuery : IQuery<int>;

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

    internal abstract class AbstractPingHandler : ICommandHandler<PingCommand, string>
    {
        public abstract Task<Result<string>> HandleAsync(PingCommand command, CancellationToken cancellationToken);
    }
}
