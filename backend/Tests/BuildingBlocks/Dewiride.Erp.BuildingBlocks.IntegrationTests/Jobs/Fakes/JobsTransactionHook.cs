using System.Data.Common;
using Dewiride.Erp.BuildingBlocks.Jobs.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Jobs.Fakes;

// Runs an action of the test once, as the next transaction of its host's jobs context begins or right after the next one
// commits, so a test puts a concurrent request, the start of a shutdown or a database that refuses a write at that point of a
// transition. Each host has a hook of its own, and a test arms the hook of the host whose transition it interrupts.
public sealed class JobsTransactionHook : DbTransactionInterceptor
{
    private Func<Task>? _beforeNextStart;

    private Func<Task>? _afterNextCommit;

    public static IServiceCollection Register(IServiceCollection services) =>
        services
            .AddSingleton<JobsTransactionHook>()
            .ConfigureDbContext<JobsDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<JobsTransactionHook>()));

    public static JobsTransactionHook Of(IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Services.GetRequiredService<JobsTransactionHook>();
    }

    public void BeforeNextStart(Func<Task> action) => Volatile.Write(ref _beforeNextStart, action);

    public void AfterNextCommit(Func<Task> action) => Volatile.Write(ref _afterNextCommit, action);

    public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
        DbConnection connection,
        TransactionStartingEventData eventData,
        InterceptionResult<DbTransaction> result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _beforeNextStart, null) is { } action)
        {
            await action();
        }

        return result;
    }

    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _afterNextCommit, null) is { } action)
        {
            await action();
        }
    }
}
