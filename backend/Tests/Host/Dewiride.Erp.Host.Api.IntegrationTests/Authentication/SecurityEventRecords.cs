using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Authentication;

// Every test host of the process writes to the one test database, so a test reads only the security events of its own
// request, by the correlation id its answer carries.
internal static class SecurityEventRecords
{
    public static async Task<List<SecurityEventEntry>> OfAsync(IServiceProvider services, HttpResponseMessage response)
    {
        var correlationId = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
        using var scope = services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AuditingDbContext>().SecurityEvents
            .Where(record => record.CorrelationId == correlationId)
            .OrderBy(record => record.OccurredAt)
            .Select(record => new SecurityEventEntry(record.Kind, record.Detail, record.ActorObjectId, record.ClientApplicationId, record.ClientAddress, record.CorrelationId))
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
