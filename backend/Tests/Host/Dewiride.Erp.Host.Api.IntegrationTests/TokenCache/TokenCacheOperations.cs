namespace Dewiride.Erp.Host.Api.IntegrationTests.TokenCache;

[Flags]
internal enum TokenCacheOperations
{
    None = 0,
    Read = 1,
    Write = 2,
    Remove = 4,
    Refresh = 8,
    All = Read | Write | Remove | Refresh,
}
