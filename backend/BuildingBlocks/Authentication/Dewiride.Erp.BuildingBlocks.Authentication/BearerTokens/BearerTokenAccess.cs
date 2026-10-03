namespace Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;

// A person's token reaches a route with one of its scopes, an application's own token with one of its application roles.
// Those must be app roles that only applications can be assigned: a person's token carries the person's app roles in the
// same roles claim.
public sealed class BearerTokenAccess
{
    public BearerTokenAccess(IReadOnlyCollection<string> scopes, IReadOnlyCollection<string> applicationRoles)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(applicationRoles);

        if (scopes.Count == 0 && applicationRoles.Count == 0)
        {
            throw new ArgumentException("A route that takes bearer tokens grants at least one scope or application role.", nameof(scopes));
        }

        if (scopes.Concat(applicationRoles).FirstOrDefault(value => string.IsNullOrEmpty(value) || value.Any(char.IsWhiteSpace)) is { } malformed)
        {
            throw new ArgumentException($"'{malformed}' is not a scope or application role: each is one word without white space.", nameof(scopes));
        }

        Scopes = [.. scopes];
        ApplicationRoles = [.. applicationRoles];
    }

    public IReadOnlyList<string> Scopes { get; }

    public IReadOnlyList<string> ApplicationRoles { get; }
}
