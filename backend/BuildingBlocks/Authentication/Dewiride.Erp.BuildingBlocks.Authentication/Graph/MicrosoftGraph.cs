namespace Dewiride.Erp.BuildingBlocks.Authentication.Graph;

public static class MicrosoftGraph
{
    public const string HttpClientName = "Erp.MicrosoftGraph";

    // Graph answers $search on users only as an advanced query, which this header asks for.
    public const string ConsistencyLevelHeader = "ConsistencyLevel";

    public const string EventualConsistency = "eventual";

    // entra.sh grants this delegated permission for the whole organisation (scripts/azure/lib/graph.sh), so reading the
    // directory never asks an administrator for consent.
    public const string ReadBasicProfilesScope = "https://graph.microsoft.com/User.ReadBasic.All";

    public static Uri BaseAddress { get; } = new("https://graph.microsoft.com/v1.0/");
}
