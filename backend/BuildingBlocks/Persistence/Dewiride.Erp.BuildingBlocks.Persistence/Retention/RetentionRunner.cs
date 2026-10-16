using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Retention;

// A sweep that fails is logged and the others still run: a deletion that can wait never stops a deployment, and the next run
// deletes what the failed one left.
public sealed partial class RetentionRunner
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<RetentionRunner> _logger;

    public RetentionRunner(IServiceScopeFactory scopeFactory, IEnumerable<RetentionSweepRegistration> registrations, ILogger<RetentionRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var list = registrations.ToList();
        var duplicate = list.GroupBy(registration => registration.SweepType).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Retention sweep {duplicate.Key.Name} is registered more than once.");
        }

        _scopeFactory = scopeFactory;
        _logger = logger;
        Sweeps = [.. list.OrderBy(registration => registration.SweepType.FullName, StringComparer.Ordinal)];
    }

    public IReadOnlyList<RetentionSweepRegistration> Sweeps { get; }

    public async Task<RetentionOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var failed = 0;
        foreach (var registration in Sweeps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var sweep = (IRetentionSweep)scope.ServiceProvider.GetRequiredService(registration.SweepType);
                await sweep.SweepAsync(cancellationToken).ConfigureAwait(false);
                LogSwept(registration.SweepType.Name);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                failed++;
                LogFailed(exception, registration.SweepType.Name);
            }
        }

        return new RetentionOutcome(Sweeps.Count - failed, failed);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ran retention sweep {Sweep}.")]
    private partial void LogSwept(string sweep);

    [LoggerMessage(Level = LogLevel.Error, Message = "Retention sweep {Sweep} failed; the next run deletes what it left.")]
    private partial void LogFailed(Exception exception, string sweep);
}
