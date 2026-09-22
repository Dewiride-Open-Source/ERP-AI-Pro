using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Events;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_EventWithTwoHandlers_InvokesBothInRegistrationOrderWithTheTypedEvent()
    {
        var log = new List<string>();
        await using var provider = new ServiceCollection()
            .AddSingleton(log)
            .AddScoped<IDomainEventHandler<Opened>, FirstOpenedHandler>()
            .AddScoped<IDomainEventHandler<Opened>, SecondOpenedHandler>()
            .AddScoped<IDomainEventHandler<Closed>, ClosedHandler>()
            .AddScoped<DomainEventDispatcher>()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>().DispatchAsync(new Opened(DateTimeOffset.UnixEpoch, "door"), TestContext.Current.CancellationToken);

        Assert.Equal(["first:door", "second:door"], log);
    }

    [Fact]
    public async Task DispatchAsync_EventWithoutHandlers_Completes()
    {
        await using var provider = new ServiceCollection().AddScoped<DomainEventDispatcher>().BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>().DispatchAsync(new Closed(DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrows_PropagatesTheException()
    {
        await using var provider = new ServiceCollection()
            .AddSingleton(new List<string>())
            .AddScoped<IDomainEventHandler<Closed>, ClosedHandler>()
            .AddScoped<DomainEventDispatcher>()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>().DispatchAsync(new Closed(DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken));
    }

    private sealed record Opened(DateTimeOffset OccurredOn, string Name) : IDomainEvent;

    private sealed record Closed(DateTimeOffset OccurredOn) : IDomainEvent;

    private sealed class FirstOpenedHandler(List<string> log) : IDomainEventHandler<Opened>
    {
        public Task HandleAsync(Opened domainEvent, CancellationToken cancellationToken)
        {
            log.Add($"first:{domainEvent.Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondOpenedHandler(List<string> log) : IDomainEventHandler<Opened>
    {
        public Task HandleAsync(Opened domainEvent, CancellationToken cancellationToken)
        {
            log.Add($"second:{domainEvent.Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class ClosedHandler : IDomainEventHandler<Closed>
    {
        public Task HandleAsync(Closed domainEvent, CancellationToken cancellationToken) => throw new InvalidOperationException("closed");
    }
}
