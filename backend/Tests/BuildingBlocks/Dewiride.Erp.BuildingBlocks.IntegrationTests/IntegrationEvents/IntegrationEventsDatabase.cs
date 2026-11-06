using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Application.Origins;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Relay;
using Dewiride.Erp.BuildingBlocks.IntegrationEvents.Subscribing;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Events;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Consumers;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Integration.Publishers;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Receipts;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents.Sample.Shipments;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Fakes;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Jobs;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Migrations;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Dewiride.Erp.Testing.Jobs;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.IntegrationEvents;

// Two runners of one installation over the test process's database, as JobsDatabase builds them, each composing the sample
// context with its outbox and processed records, its publisher and two subscribers named for this fixture alone, so no other
// test's host claims their deliveries. A test passes on and runs its own events by id, because every host of the process shares
// the outbox.
internal sealed class IntegrationEventsDatabase : IAsyncDisposable
{
    private static readonly SemaphoreSlim MigrationGate = new(1, 1);

    private static bool _migrated;

    private IntegrationEventsDatabase(IHost first, IHost second, string recorder, string printer)
    {
        First = first;
        Second = second;
        Recorder = recorder;
        Printer = printer;
    }

    public IHost First { get; }

    public IHost Second { get; }

    public string Recorder { get; }

    public string Printer { get; }

    public SubscriberScript Script => First.Services.GetRequiredService<SubscriberScript>();

    public FakeTimeProvider FirstClock => First.Services.GetRequiredService<FakeTimeProvider>();

    public FakeTimeProvider SecondClock => Second.Services.GetRequiredService<FakeTimeProvider>();

    public static async Task<IntegrationEventsDatabase> CreateAsync(JobRetryPolicy? retry = null)
    {
        var recorder = JobsDatabase.NewName("receipt-recorder");
        var printer = JobsDatabase.NewName("label-printer");
        var script = new SubscriberScript();
        var published = new PublishedEvents();
        var database = new IntegrationEventsDatabase(
            Build("runner-a", recorder, printer, script, published, retry ?? JobRetryPolicy.Default),
            Build("runner-b", recorder, printer, script, published, retry ?? JobRetryPolicy.Default),
            recorder,
            printer);
        await MigrateOnceAsync(database.First);

        return database;
    }

    public static TestActorContext ActorOf(IHost host) => host.Services.GetRequiredService<TestActorContext>();

    public static TestRequestOrigin OriginOf(IHost host) => host.Services.GetRequiredService<TestRequestOrigin>();

    public static ApplicationLifetime LifetimeOf(IHost host) => (ApplicationLifetime)host.Services.GetRequiredService<IHostApplicationLifetime>();

    public static FakeLogCollector LogsOf(IHost host) => host.Services.GetRequiredService<FakeLogCollector>();

    public void Advance(TimeSpan by)
    {
        FirstClock.Advance(by);
        SecondClock.Advance(by);
    }

    public async Task<Result<string>> RunUnitAsync(Func<SampleDbContext, IServiceProvider, CancellationToken, Task<Result<string>>> body, IHost? host = null)
    {
        ArgumentNullException.ThrowIfNull(body);

        await using var scope = (host ?? First).Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        return await EfUnitOfWork.RunAsync(
            context,
            scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>(),
            scope.ServiceProvider.GetRequiredService<UnitOfWorkSignal>(),
            token => body(context, scope.ServiceProvider, token),
            TestContext.Current.CancellationToken);
    }

    public async Task<(SampleShipmentId Shipment, IntegrationEventId Event)> DispatchAsync(SampleShipmentId? shipment = null, int parcels = 3, IHost? host = null)
    {
        var id = shipment ?? SampleShipmentId.Create();
        var result = await RunUnitAsync(
            (context, _, _) =>
            {
                context.Shipments.Add(SampleShipment.Dispatch(id, parcels, FirstClock.GetUtcNow()));

                return Task.FromResult(Result.Success("dispatched"));
            },
            host);
        Assert.True(result.IsSuccess);

        return (id, First.Services.GetRequiredService<PublishedEvents>().Of(id));
    }

    public async Task<IntegrationEventDelivery> DeliveryOf(IntegrationEventId id)
    {
        var row = await OutboxRowAsync(id) ?? throw new InvalidOperationException($"The event {id.Value} is not in the outbox.");
        using var document = JsonDocument.Parse(row.Payload);

        return new IntegrationEventDelivery(id.Value, row.EventName, SampleDbContext.SchemaName, row.OccurredAt, row.CorrelationId, document.RootElement.Clone());
    }

    public async Task<JobRunId> EnqueueAsync<THandler>(IntegrationEventDelivery delivery, string key)
        where THandler : class, IIntegrationEventHandler<ShipmentDispatched>
    {
        await using var scope = First.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IJobQueue>()
            .EnqueueAsync<IntegrationEventDeliveryJob<ShipmentDispatched, THandler>, IntegrationEventDelivery>(delivery, key, TestContext.Current.CancellationToken);
    }

    public bool Published(SampleShipmentId shipment) => First.Services.GetRequiredService<PublishedEvents>().Contains(shipment);

    public Task RelayAsync(IntegrationEventId id) => RelayAsync(First, id);

    public static Task RelayAsync(IHost host, IntegrationEventId id)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Services.GetRequiredService<OutboxRelay>().RelayOneAsync(typeof(SampleDbContext), id, TestContext.Current.CancellationToken);
    }

    public Task<JobRunOutcome> RunAsync(JobRunId runId) => RunAsync(First, runId);

    public static Task<JobRunOutcome> RunAsync(IHost host, JobRunId runId)
    {
        ArgumentNullException.ThrowIfNull(host);

        return new JobRunnerDriver(host.Services).RunAsync(runId, TestContext.Current.CancellationToken);
    }

    public async Task<T> QueryAsync<T>(Func<SampleDbContext, CancellationToken, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var scope = First.Services.CreateAsyncScope();

        return await query(scope.ServiceProvider.GetRequiredService<SampleDbContext>(), TestContext.Current.CancellationToken);
    }

    public async Task<T> QueryJobsAsync<T>(Func<JobsDbContext, CancellationToken, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var scope = First.Services.CreateAsyncScope();

        return await query(scope.ServiceProvider.GetRequiredService<JobsDbContext>(), TestContext.Current.CancellationToken);
    }

    public Task<OutboxMessage?> OutboxRowAsync(IntegrationEventId id) =>
        QueryAsync((context, token) => context.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, token));

    public Task<List<SampleReceipt>> ReceiptsAsync(SampleShipmentId shipment) =>
        QueryAsync((context, token) => context.Receipts.AsNoTracking().Where(r => r.ShipmentId == shipment.Value).OrderBy(r => r.RecordedBy).ToListAsync(token));

    public Task<List<ProcessedMessage>> ProcessedAsync(IntegrationEventId id) =>
        QueryAsync((context, token) => context.ProcessedMessages.AsNoTracking().Where(m => m.EventId == id).OrderBy(m => m.Subscriber).ToListAsync(token));

    public Task<List<JobRun>> DeliveriesAsync(IntegrationEventId id) =>
        QueryJobsAsync((context, token) =>
        {
            var key = OutboxRelay.KeyOf(id);

            return context.Runs.AsNoTracking().Where(r => r.IdempotencyKey == key).OrderBy(r => r.JobName).ToListAsync(token);
        });

    public async Task<JobRun> DeliveryAsync(IntegrationEventId id, string subscriber) =>
        Assert.Single(await DeliveriesAsync(id), run => run.JobName == subscriber);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(First);
        await DisposeAsync(Second);
    }

    private static async Task DisposeAsync(IHost host)
    {
        if (host is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
        else
        {
            host.Dispose();
        }
    }

    private static async Task MigrateOnceAsync(IHost host)
    {
        await MigrationGate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (!_migrated)
            {
                await host.Services.GetRequiredService<DatabaseMigrator>().MigrateAllAsync(TestContext.Current.CancellationToken);
                _migrated = true;
            }
        }
        finally
        {
            MigrationGate.Release();
        }
    }

    private static IHost Build(string runner, string recorder, string printer, SubscriberScript script, PublishedEvents published, JobRetryPolicy retry)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.InMemorySource,
            [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}"] = SqlTestDatabase.Current.ConnectionString,
        });
        var clock = new FakeTimeProvider(JobsDatabase.DefaultStart);
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug));
        builder.Services.AddFakeLogging();
        builder.Services.AddMetrics();
        builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<TestActorContext>();
        builder.Services.AddSingleton<IActorContext>(provider => provider.GetRequiredService<TestActorContext>());
        builder.Services.AddSingleton<TestRequestOrigin>();
        builder.Services.AddScoped<IRequestOrigin>(provider => provider.GetRequiredService<TestRequestOrigin>());
        builder.Services.AddSingleton(new JobRunnerInstance(runner));
        builder.Services.AddSingleton(script);
        builder.Services.AddSingleton(published);
        builder.Services.AddErpPersistenceCore();
        builder.Services.AddModuleDbContext<SampleDbContext>(SampleDbContext.SchemaName);
        builder.Services.AddScoped<IDomainEventHandler<SampleShipmentDispatched>, ShipmentDispatchedPublisher>();
        builder.AddErpJobs();
        builder.AddErpJobRunner();
        builder.AddErpIntegrationEvents();
        builder.Services.AddIntegrationEventHandler<ShipmentDispatched, ShipmentReceiptRecorder>(JobsDatabase.Definition(recorder, retry));
        builder.Services.AddIntegrationEventHandler<ShipmentDispatched, ShipmentLabelPrinter>(JobsDatabase.Definition(printer, retry));

        return builder.Build();
    }
}
