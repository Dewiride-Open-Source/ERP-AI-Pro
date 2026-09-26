using System.Diagnostics;
using OpenTelemetry;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Telemetry;

// SQL Server quotes the offending value in some error messages (a duplicate key, a truncated string) and the SqlClient
// instrumentation copies the message into the span status. Clearing the description keeps values out of traces, while
// error.type and db.response.status_code still identify the failure. It must be registered before any exporter, because
// processors run in registration order and an exporter reads the status when its own OnEnd runs.
internal sealed class SqlErrorStatusProcessor : BaseProcessor<Activity>
{
    public const string SqlClientSourceName = "OpenTelemetry.Instrumentation.SqlClient";

    public override void OnEnd(Activity data)
    {
        if (data.Source.Name == SqlClientSourceName && data.Status == ActivityStatusCode.Error && data.StatusDescription is not null)
        {
            data.SetStatus(ActivityStatusCode.Error);
        }
    }
}
