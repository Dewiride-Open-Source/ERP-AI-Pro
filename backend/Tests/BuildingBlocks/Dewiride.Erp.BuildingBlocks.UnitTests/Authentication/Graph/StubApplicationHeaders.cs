using System.Security.Claims;
using Microsoft.Identity.Abstractions;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Graph;

internal sealed class StubApplicationHeaders(Exception? refusal = null) : IAuthorizationHeaderProvider
{
    public const string AuthorizationHeader = "Bearer test-application-token";

    public int Requests { get; private set; }

    public string? Scope { get; private set; }

    public AuthorizationHeaderProviderOptions? Options { get; private set; }

    public Task<string> CreateAuthorizationHeaderForAppAsync(string scopes, AuthorizationHeaderProviderOptions? downstreamApiOptions = null, CancellationToken cancellationToken = default)
    {
        Requests++;
        Scope = scopes;
        Options = downstreamApiOptions;

        return refusal is null ? Task.FromResult(AuthorizationHeader) : Task.FromException<string>(refusal);
    }

    public Task<string> CreateAuthorizationHeaderForUserAsync(IEnumerable<string> scopes, AuthorizationHeaderProviderOptions? authorizationHeaderProviderOptions = null, ClaimsPrincipal? claimsPrincipal = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The account check never asks for a token on a person's behalf.");

    public Task<string> CreateAuthorizationHeaderAsync(IEnumerable<string> scopes, AuthorizationHeaderProviderOptions? options = null, ClaimsPrincipal? claimsPrincipal = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The account check asks for an application token.");
}
