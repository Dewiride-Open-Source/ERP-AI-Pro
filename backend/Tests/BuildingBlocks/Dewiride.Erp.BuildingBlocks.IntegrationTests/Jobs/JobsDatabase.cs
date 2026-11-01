using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Configuration.Sources;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Jobs;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Options;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs;

// Two runners of one installation over the test process's database, each a host of its own that is never started, with the jobs
// the test gives them and a clock of its own that starts where the other's does: a test moves both, or one alone to stand for a
// runner whose clock or lease renewals stopped. The test's registrations are made once and given to both hosts, so the two
// define the same jobs. Every host of the process shares the database, so each test names its jobs with a suffix of its own, and
// a runner claims only the runs of the jobs it defines.
internal sealed class JobsDatabase : IAsyncDisposable
{
    public static readonly DateTimeOffset DefaultStart = new(2026, 11, 1, 6, 30, 0, TimeSpan.Zero);

    private JobsDatabase(IHost first, IHost second)
    {
        First = first;
        Second = second;
    }

    public IHost First { get; }

    public IHost Second { get; }

    public FakeTimeProvider FirstClock => First.Services.GetRequiredService<FakeTimeProvider>();

    public FakeTimeProvider SecondClock => Second.Services.GetRequiredService<FakeTimeProvider>();

    public JobRunner FirstRunner => First.Services.GetRequiredService<JobRunner>();

    public JobRunner SecondRunner => Second.Services.GetRequiredService<JobRunner>();

    public static string NewName(string scenario) => $"test.jobs.{scenario}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";

    public static string NewKey() => $"test:{Guid.CreateVersion7()}";

    public static JobDefinition Definition(string name, JobRetryPolicy? retry = null, TimeSpan? attemptTimeout = null) =>
        new(name, "Test job", "Runs for a test.", retry ?? JobRetryPolicy.Default, attemptTimeout ?? TimeSpan.FromMinutes(10));

    public static JobsDatabase Create(
        Action<IServiceCollection> configure,
        DateTimeOffset? start = null,
        IReadOnlyDictionary<string, string?>? settings = null,
        string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var at = start ?? DefaultStart;
        var values = settings ?? new Dictionary<string, string?>();
        var database = connectionString ?? SqlTestDatabase.Current.ConnectionString;

        var registrations = new ServiceCollection();
        configure(registrations);

        return new JobsDatabase(Build("runner-a", at, registrations, values, database), Build("runner-b", at, registrations, values, database));
    }

    public static TestActorContext ActorOf(IHost host) => host.Services.GetRequiredService<TestActorContext>();

    public static ApplicationLifetime LifetimeOf(IHost host) => (ApplicationLifetime)host.Services.GetRequiredService<IHostApplicationLifetime>();

    public static FakeLogCollector LogsOf(IHost host) => host.Services.GetRequiredService<FakeLogCollector>();

    public void Advance(TimeSpan by)
    {
        FirstClock.Advance(by);
        SecondClock.Advance(by);
    }

    public async Task<JobRunId> EnqueueAsync<TJob>(string idempotencyKey)
        where TJob : class, IJob
    {
        await using var scope = First.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IJobQueue>().EnqueueAsync<TJob>(idempotencyKey, TestContext.Current.CancellationToken);
    }

    public async Task<T> QueryAsync<T>(Func<JobsDbContext, CancellationToken, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var scope = First.Services.CreateAsyncScope();

        return await query(scope.ServiceProvider.GetRequiredService<JobsDbContext>(), TestContext.Current.CancellationToken);
    }

    public Task<JobRun> RunAsync(JobRunId id) =>
        QueryAsync((context, cancellationToken) => context.Runs.AsNoTracking().SingleAsync(r => r.Id == id, cancellationToken));

    public Task<List<JobRunEvent>> EventsAsync(JobRunId id) =>
        QueryAsync((context, cancellationToken) => context.Events.AsNoTracking().Where(e => e.RunId == id).OrderBy(e => e.OccurredAt).ThenBy(e => e.Kind).ToListAsync(cancellationToken));

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

    private static IHost Build(string runner, DateTimeOffset start, ServiceCollection registrations, IReadOnlyDictionary<string, string?> settings, string connectionString)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(settings, StringComparer.OrdinalIgnoreCase)
        {
            [ErpConfigurationSourceResolver.SourceSetting] = ErpConfigurationSourceResolver.InMemorySource,
            [$"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)}"] = connectionString,
        });
        var clock = new FakeTimeProvider(start);
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug));
        builder.Services.AddFakeLogging();
        builder.Services.AddMetrics();
        builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<TestActorContext>();
        builder.Services.AddSingleton<IActorContext>(provider => provider.GetRequiredService<TestActorContext>());
        builder.Services.AddSingleton(new JobRunnerInstance(runner));
        builder.Services.AddErpPersistenceCore();
        foreach (var registration in registrations)
        {
            builder.Services.Add(registration);
        }

        builder.AddErpJobs();
        builder.AddErpJobRunner();

        return builder.Build();
    }
}
