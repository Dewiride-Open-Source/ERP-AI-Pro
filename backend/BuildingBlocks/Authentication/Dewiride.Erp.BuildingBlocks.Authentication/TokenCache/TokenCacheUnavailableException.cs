namespace Dewiride.Erp.BuildingBlocks.Authentication.TokenCache;

// The message never names the cache key, which is the person's account.
internal sealed class TokenCacheUnavailableException : Exception
{
    public const string DefaultMessage = "The token cache could not be read or written.";

    public TokenCacheUnavailableException()
        : base(DefaultMessage)
    {
    }

    public TokenCacheUnavailableException(Exception innerException)
        : base(DefaultMessage, innerException)
    {
    }

    public TokenCacheUnavailableException(string message)
        : base(message)
    {
    }

    public TokenCacheUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
