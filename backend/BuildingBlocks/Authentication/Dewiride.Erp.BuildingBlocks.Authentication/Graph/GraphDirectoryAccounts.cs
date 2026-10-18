using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Client;
using Polly;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Graph;

// The token is the application's own, acquired with the registration and certificate of the sign-in scheme, because whether
// an account is enabled is beyond the basic profile a person's directory token reads: it carries the application permission
// User.Read.All. Graph takes at most 15 values in an in filter (Microsoft Graph, "Known issues", query parameters), so the ids
// are asked 15 at a time, and an id its request's answer leaves out belongs to an account the directory does not hold. A next
// page is followed only on Graph itself, the one host the token is for; an answer of more pages than a request can ask ids, or
// one naming an account it was not asked about or not saying whether it is enabled, is no answer Graph gives. Entra refusing
// the token (400, 401 or 403 from its token endpoint) or Graph refusing it (401 or 403) is a refusal; Entra or Graph
// unreachable, or still failing or throttling once the resilience handler gives up, is unavailable; any other answer is a fault
// of the ERP and fails the request. The ids travel in the query string, which the HTTP client's logs and traces redact, and the
// logs here name the operation, the status and the error codes only.
internal sealed partial class GraphDirectoryAccounts(
    HttpClient client,
    IAuthorizationHeaderProvider authorizationHeaders,
    ILogger<GraphDirectoryAccounts> logger) : IDirectoryAccounts
{
    public const int MaxIdsPerRequest = 15;

    private const string Operation = "account check";

    private const string SelectedProperties = "id,accountEnabled";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly AuthorizationHeaderProviderOptions TokenOptions = new()
    {
        AcquireTokenOptions = new AcquireTokenOptions { AuthenticationOptionsName = OpenIdConnectDefaults.AuthenticationScheme },
    };

    public async Task<Result<IReadOnlyDictionary<Guid, DirectoryAccountState>>> StatesOfAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectIds);

        var states = new Dictionary<Guid, DirectoryAccountState>();
        var asked = objectIds.Distinct().ToArray();
        if (asked.Length == 0)
        {
            return states;
        }

        var authorization = await AuthorizationAsync(cancellationToken).ConfigureAwait(false);
        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        foreach (var chunk in asked.Chunk(MaxIdsPerRequest))
        {
            var enabled = await EnabledAccountsAsync(chunk, authorization.Value, cancellationToken).ConfigureAwait(false);
            if (enabled.IsFailure)
            {
                return enabled.Error!;
            }

            foreach (var objectId in chunk)
            {
                states[objectId] = StateOf(enabled.Value, objectId);
            }
        }

        return states;
    }

    internal static string FilterPath(IEnumerable<Guid> objectIds)
    {
        var filter = $"id in ({string.Join(',', objectIds.Select(objectId => $"'{objectId:D}'"))})";

        return $"users?$filter={Uri.EscapeDataString(filter)}&$select={SelectedProperties}";
    }

    internal static Uri NextPage(string nextLink) =>
        Uri.TryCreate(nextLink, UriKind.Absolute, out var next)
        && string.Equals(next.GetLeftPart(UriPartial.Authority), MicrosoftGraph.BaseAddress.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal)
            ? next
            : throw new InvalidOperationException($"Microsoft Graph answered the directory {Operation} with a next page outside {MicrosoftGraph.BaseAddress.GetLeftPart(UriPartial.Authority)}, where the application's token is never sent.");

    private static DirectoryAccountState StateOf(Dictionary<Guid, bool> enabled, Guid objectId)
    {
        if (!enabled.TryGetValue(objectId, out var isEnabled))
        {
            return DirectoryAccountState.Deleted;
        }

        return isEnabled ? DirectoryAccountState.Enabled : DirectoryAccountState.Disabled;
    }

    private static bool IsRefusal(MsalServiceException exception) =>
        exception is not MsalThrottledServiceException && exception.StatusCode is (int)HttpStatusCode.BadRequest or (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden;

    private static async Task<string?> GraphErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<GraphErrorBody>(Json, cancellationToken).ConfigureAwait(false);

            return body?.Error?.Code;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<Result<AuthenticationHeaderValue>> AuthorizationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await authorizationHeaders.CreateAuthorizationHeaderForAppAsync(MicrosoftGraph.ApplicationScope, TokenOptions, cancellationToken).ConfigureAwait(false);

            return AuthenticationHeaderValue.Parse(authorization);
        }
        catch (MsalServiceException exception) when (IsRefusal(exception))
        {
            LogTokenRefused(logger, Operation, exception.ErrorCode, exception.StatusCode);
            return DirectoryErrors.AccountsAccessDenied;
        }
        catch (MsalException exception)
        {
            LogTokenUnavailable(logger, Operation, exception.ErrorCode);
            return DirectoryErrors.Unavailable;
        }
        catch (HttpRequestException exception)
        {
            LogTokenEndpointUnreachable(logger, exception, Operation);
            return DirectoryErrors.Unavailable;
        }
    }

    private async Task<Result<Dictionary<Guid, bool>>> EnabledAccountsAsync(Guid[] chunk, AuthenticationHeaderValue authorization, CancellationToken cancellationToken)
    {
        var enabled = new Dictionary<Guid, bool>();
        var address = new Uri(FilterPath(chunk), UriKind.Relative);
        for (var page = 1; page <= MaxIdsPerRequest; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.Authorization = authorization;
            var sent = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (sent.IsFailure)
            {
                return sent.Error!;
            }

            using var response = sent.Value;
            if (!response.IsSuccessStatusCode)
            {
                return await RefusalAsync(response, cancellationToken).ConfigureAwait(false);
            }

            var accounts = await response.Content.ReadFromJsonAsync<AccountsPage>(Json, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Microsoft Graph answered the directory {Operation} without a body.");
            foreach (var account in accounts.Value ?? [])
            {
                if (!Guid.TryParse(account.Id, out var objectId) || !chunk.Contains(objectId) || account.AccountEnabled is not { } isEnabled)
                {
                    throw new InvalidOperationException($"Microsoft Graph answered the directory {Operation} with an account it was not asked about or without whether the account is enabled.");
                }

                enabled[objectId] = isEnabled;
            }

            if (accounts.NextLink is null)
            {
                return enabled;
            }

            address = NextPage(accounts.NextLink);
        }

        throw new InvalidOperationException($"Microsoft Graph answered the directory {Operation} of {chunk.Length} accounts with more than {MaxIdsPerRequest} pages.");
    }

    private async Task<Result<HttpResponseMessage>> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            LogUnreachable(logger, exception, Operation);
            return DirectoryErrors.Unavailable;
        }
        catch (ExecutionRejectedException exception)
        {
            LogUnreachable(logger, exception, Operation);
            return DirectoryErrors.Unavailable;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, exception, Operation);
            return DirectoryErrors.Unavailable;
        }
    }

    private async Task<Error> RefusalAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var code = await GraphErrorCodeAsync(response, cancellationToken).ConfigureAwait(false);
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                LogRefused(logger, Operation, status, code);
                return DirectoryErrors.AccountsAccessDenied;
            case HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError:
                LogUnavailable(logger, Operation, status, code);
                return DirectoryErrors.Unavailable;
            default:
                throw new InvalidOperationException($"Microsoft Graph answered the directory {Operation} with {status} {code}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Entra refused the application token of the directory {Operation}: {ErrorCode} ({StatusCode})")]
    private static partial void LogTokenRefused(ILogger logger, string operation, string errorCode, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Entra could not issue the application token of the directory {Operation}: {ErrorCode}")]
    private static partial void LogTokenUnavailable(ILogger logger, string operation, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Entra could not be reached for the application token of the directory {Operation}")]
    private static partial void LogTokenEndpointUnreachable(ILogger logger, Exception exception, string operation);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Graph refused the directory {Operation} with {StatusCode} {GraphErrorCode}")]
    private static partial void LogRefused(ILogger logger, string operation, int statusCode, string? graphErrorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Graph could not answer the directory {Operation}: {StatusCode} {GraphErrorCode}")]
    private static partial void LogUnavailable(ILogger logger, string operation, int statusCode, string? graphErrorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Graph could not be reached for the directory {Operation}")]
    private static partial void LogUnreachable(ILogger logger, Exception exception, string operation);

    private sealed record AccountsPage(IReadOnlyList<GraphAccount>? Value, [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record GraphAccount(string? Id, bool? AccountEnabled);

    private sealed record GraphErrorBody(GraphError? Error);

    private sealed record GraphError(string? Code);
}
