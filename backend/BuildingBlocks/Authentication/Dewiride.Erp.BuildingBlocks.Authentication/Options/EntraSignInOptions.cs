using System.ComponentModel.DataAnnotations;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Options;

public sealed class EntraSignInOptions
{
    public const string SectionName = "Erp:Platform:Identity";

    public const string DefaultInstance = "https://login.microsoftonline.com/";

    public string? TenantId { get; set; }

    public string? ClientId { get; set; }

    public string Instance { get; set; } = DefaultInstance;

    // The browser reaches the API only through the web app's rewrite, which hands the API its own host, so the redirect
    // URIs Entra sees are built from this origin and never from the request.
    public string? WebOrigin { get; set; }

    public string? ClientCertificate { get; set; }

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(12);
}
