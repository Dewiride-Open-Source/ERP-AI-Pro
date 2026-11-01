using System.Collections.Concurrent;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Jobs.Definitions;
using Dewiride.Erp.BuildingBlocks.Jobs.Queue;
using Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Jobs.Running;

// Activates the job in the attempt's scope and runs it there. The scope must answer the system as its actor: the books' trail
// credits whoever the scope names with the job's changes, and a scope made inside a request would name the person of the
// request, so any other actor stops the attempt before the job is built.
internal sealed partial class JobInvoker(ILogger<JobInvoker> logger)
{
    private readonly ConcurrentDictionary<Type, ObjectFactory> _factories = new();

    public async Task<Result> InvokeAsync(JobRegistration registration, string? payload, JobContext context, IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(services);

        if (services.GetRequiredService<IActorContext>().ActorId != ActorIds.System)
        {
            LogRefusedActor(registration.Name);
            return JobAttemptErrors.ActorNotSystem;
        }

        object? input = null;
        if (registration.PayloadType is { } payloadType && !JobPayloads.TryDeserialize(payload, payloadType, out input))
        {
            return JobAttemptErrors.PayloadUnreadable;
        }

        var job = _factories.GetOrAdd(registration.JobType, static type => ActivatorUtilities.CreateFactory(type, Type.EmptyTypes))(services, null);
        try
        {
            return await registration.Invoke(job, input, context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (job is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (job is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "An attempt of {JobName} would have run as someone other than the system, so it did not run")]
    private partial void LogRefusedActor(string jobName);
}
