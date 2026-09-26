namespace Dewiride.Erp.BuildingBlocks.Observability.Telemetry;

public static class TelemetryMeters
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.MemoryPool",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Diagnostics",
        "Microsoft.AspNetCore.RateLimiting",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authorization",
        "System.Net.Http",
        "System.Net.NameResolution",
        "System.Runtime",
        $"{OpenTelemetrySetup.ActivitySourcePrefix}.*",
    ];
}
