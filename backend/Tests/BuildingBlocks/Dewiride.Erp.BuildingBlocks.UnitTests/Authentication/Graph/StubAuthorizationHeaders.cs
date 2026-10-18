using System.Security.Claims;
using Microsoft.Identity.Abstractions;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

internal sealed class StubAuthorizationHeaders(Exception? refusal = null) : IAuthorizationHeaderProvider
{
    public const string AuthorizationHeader = "Bearer test-graph-token";

    public IReadOnlyList<string> Scopes { get; private set; } = [];

    public AuthorizationHeaderProviderOptions? Options { get; private set; }

    public ClaimsPrincipal? Principal { get; private set; }

    public Task<string> CreateAuthorizationHeaderForUserAsync(IEnumerable<string> scopes, AuthorizationHeaderProviderOptions? authorizationHeaderProviderOptions = null, ClaimsPrincipal? claimsPrincipal = null, CancellationToken cancellationToken = default)
    {
        Scopes = [.. scopes];
        Options = authorizationHeaderProviderOptions;
        Principal = claimsPrincipal;

        return refusal is null ? Task.FromResult(AuthorizationHeader) : Task.FromException<string>(refusal);
    }

    public Task<string> CreateAuthorizationHeaderForAppAsync(string scopes, AuthorizationHeaderProviderOptions? downstreamApiOptions = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The directory never asks for an application token.");

    public Task<string> CreateAuthorizationHeaderAsync(IEnumerable<string> scopes, AuthorizationHeaderProviderOptions? options = null, ClaimsPrincipal? claimsPrincipal = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The directory asks for a token for the user.");
}
