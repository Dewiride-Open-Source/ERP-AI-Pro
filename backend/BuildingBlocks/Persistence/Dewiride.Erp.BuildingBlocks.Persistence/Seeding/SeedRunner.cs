using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Seeding;

public sealed partial class SeedRunner
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<SeedRunner> _logger;

    public SeedRunner(IServiceScopeFactory scopeFactory, IEnumerable<SeederRegistration> registrations, ILogger<SeedRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var list = registrations.ToList();
        var duplicate = list.GroupBy(registration => registration.SeederType).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Seeder {duplicate.Key.Name} is registered more than once.");
        }

        _scopeFactory = scopeFactory;
        _logger = logger;
        Seeders = [.. list.OrderBy(registration => registration.Order).ThenBy(registration => registration.SeederType.FullName, StringComparer.Ordinal)];
    }

    public IReadOnlyList<SeederRegistration> Seeders { get; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in Seeders)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var seeder = (ISeeder)scope.ServiceProvider.GetRequiredService(registration.SeederType);
            await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
            LogSeeded(registration.SeederType.Name, registration.Order);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ran seeder {Seeder} (order {Order}).")]
    private partial void LogSeeded(string seeder, int order);
}
