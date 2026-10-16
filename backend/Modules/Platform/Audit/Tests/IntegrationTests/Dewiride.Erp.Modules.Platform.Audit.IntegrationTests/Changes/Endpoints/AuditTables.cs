using System.Data;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Testing.Sql;
using Microsoft.Data.SqlClient;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints;

// The audit tables belong to the auditing building block, whose context this module never sees, so a test reads the rows of
// its own request or record straight from the test database.
internal static class AuditTables
{
    public static async Task<List<(SecurityEventKind Kind, string? Detail, Guid? ActorObjectId)>> SecurityEventsOfAsync(HttpResponseMessage response)
    {
        var correlationId = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
        await using var connection = new SqlConnection(SqlTestDatabase.Current.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand("SELECT [Kind], [Detail], [ActorObjectId] FROM [audit].[SecurityEvents] WHERE [CorrelationId] = @correlationId", connection);
        command.Parameters.Add(new SqlParameter("@correlationId", SqlDbType.VarChar, CorrelationId.MaxLength) { Value = correlationId });
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var events = new List<(SecurityEventKind Kind, string? Detail, Guid? ActorObjectId)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            events.Add(((SecurityEventKind)reader.GetByte(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2)));
        }

        return events;
    }

    public static async Task<List<(ChangeOperation Operation, string EntityType)>> ChangeRecordsOfAsync(Guid entityId)
    {
        await using var connection = new SqlConnection(SqlTestDatabase.Current.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand("SELECT [Operation], [EntityType] FROM [audit].[ChangeRecords] WHERE [EntityId] = @entityId ORDER BY [OccurredAt]", connection);
        command.Parameters.Add(new SqlParameter("@entityId", SqlDbType.UniqueIdentifier) { Value = entityId });
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var records = new List<(ChangeOperation Operation, string EntityType)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            records.Add(((ChangeOperation)reader.GetByte(0), reader.GetString(1)));
        }

        return records;
    }
}
