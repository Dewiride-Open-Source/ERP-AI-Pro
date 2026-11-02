using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Domain;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Jobs;

// Keeps each enqueue in memory and refuses what the database queue refuses: a job nobody registered, a payload of the wrong type
// or too large, a malformed key, a key of the scheduler's, and a used key with another payload. Nothing runs until RunAllAsync,
// which runs each recorded run once, through the building block's invoker, in a scope of its own as the system.
public sealed class InProcessJobQueue(IServiceProvider services) : IJobQueue
{
    private readonly Lock _gate = new();

    private readonly List<Entry> _entries = [];

    public IReadOnlyList<InProcessJobRun> Runs
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries.Select(entry => entry.ToRun())];
            }
        }
    }

    public Task<JobRunId> EnqueueAsync<TJob>(string idempotencyKey, CancellationToken cancellationToken)
        where TJob : class, IJob
    {
        var registration = Catalog.Require(typeof(TJob), payloadType: null);
        JobKeys.EnsureEnqueueable(idempotencyKey);

        return Task.FromResult(Record(registration, idempotencyKey, payload: null, json: null));
    }

    public Task<JobRunId> EnqueueAsync<TJob, TPayload>(TPayload payload, string idempotencyKey, CancellationToken cancellationToken)
        where TJob : class, IJob<TPayload>
        where TPayload : class
    {
        var registration = Catalog.Require(typeof(TJob), typeof(TPayload));
        JobKeys.EnsureEnqueueable(idempotencyKey);

        return Task.FromResult(Record(registration, idempotencyKey, payload, JobPayloads.Serialize(payload)));
    }

    public async Task<IReadOnlyList<InProcessJobRun>> RunAllAsync(CancellationToken cancellationToken)
    {
        List<Entry> waiting;
        lock (_gate)
        {
            waiting = [.. _entries.Where(entry => !entry.Taken)];
            waiting.ForEach(entry => entry.Taken = true);
        }

        var invoker = services.GetRequiredService<JobInvoker>();
        var ran = new List<InProcessJobRun>();
        foreach (var entry in waiting)
        {
            await using var scope = services.CreateAsyncScope();
            var result = await invoker.InvokeAsync(entry.Registration, entry.Json, new JobContext(entry.Id, 1), scope.ServiceProvider, cancellationToken);
            lock (_gate)
            {
                entry.Result = result;
                ran.Add(entry.ToRun());
            }
        }

        return ran;
    }

    private JobCatalog Catalog => services.GetRequiredService<JobCatalog>();

    private JobRunId Record(JobRegistration registration, string idempotencyKey, object? payload, string? json)
    {
        lock (_gate)
        {
            var existing = _entries.Find(entry => entry.Registration.Name == registration.Name && JobKeys.Comparer.Equals(entry.IdempotencyKey, idempotencyKey));
            if (existing is not null)
            {
                return existing.Json == json
                    ? existing.Id
                    : throw new InvalidOperationException($"The job {registration.Name} already has a run with this idempotency key and a different payload.");
            }

            var entry = new Entry(JobRunId.Create(), registration, idempotencyKey, payload, json);
            _entries.Add(entry);

            return entry.Id;
        }
    }

    private sealed class Entry(JobRunId id, JobRegistration registration, string idempotencyKey, object? payload, string? json)
    {
        public JobRunId Id { get; } = id;

        public JobRegistration Registration { get; } = registration;

        public string IdempotencyKey { get; } = idempotencyKey;

        public object? Payload { get; } = payload;

        public string? Json { get; } = json;

        public bool Taken { get; set; }

        public Result? Result { get; set; }

        public InProcessJobRun ToRun() => new(Id, Registration.Name, IdempotencyKey, Payload, Result);
    }
}
