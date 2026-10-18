using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Polly;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Graph;

// The token is the signed-in person's own, acquired with the refresh token of their session, so Entra shows them only what
// they may read there. The search text travels in the query string, which the HTTP client's logs and traces redact, and is
// logged nowhere here. Entra or Graph refusing the person (consent missing, a sign-in interaction required, 401 or 403) is a
// refusal; a directory that cannot be reached, or still fails or throttles once the resilience handler gives up, is
// unavailable; any other answer is a fault of the ERP and fails the request. A person's own photo is read with User.Read, the
// least privileged of the permissions the sign-in registrations hold that reads it (Microsoft Graph, "Get profilePhoto"): the
// 240x240 size the pages show first, then the largest one, because a photo uploaded smaller, or one Entra keeps at a size of
// its own, has no 240x240 size. What Graph answered, a photo or none, is kept in process by the person's object id for
// PhotoLifetime, so the account button of every page asks Graph for a person's photo once in that time; a refusal or an
// unavailable directory is asked again at the next request.
internal sealed partial class GraphPeopleDirectory(
    HttpClient client,
    IAuthorizationHeaderProvider authorizationHeaders,
    IHttpContextAccessor httpContextAccessor,
    HybridCache cache,
    ILogger<GraphPeopleDirectory> logger) : IPeopleDirectory
{
    private const string SearchOperation = "search";

    private const string FindOperation = "lookup";

    private const string PhotoOperation = "photo";

    private const string SelectedProperties = "id,displayName,mail,userPrincipalName";

    private const string GuestMarker = "#EXT#@";

    private const string PhotoCacheKeyPrefix = "directory-photo:";

    private static readonly TimeSpan PhotoLifetime = TimeSpan.FromMinutes(15);

    private static readonly string[] SearchedProperties = ["displayName", "mail", "userPrincipalName"];

    private static readonly string[] PhotoPaths = ["me/photos/240x240/$value", "me/photo/$value"];

    private static readonly string[] DirectoryScopes = [MicrosoftGraph.ReadBasicProfilesScope];

    private static readonly string[] OwnProfileScopes = [MicrosoftGraph.ReadOwnProfileScope];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // The token acquisition options are named after the sign-in scheme; the default authenticate scheme is the policy scheme
    // that picks the cookie or the bearer handler, which has none.
    private static readonly AuthorizationHeaderProviderOptions TokenOptions = new()
    {
        AcquireTokenOptions = new AcquireTokenOptions { AuthenticationOptionsName = OpenIdConnectDefaults.AuthenticationScheme },
    };

    private static readonly HybridCacheEntryOptions CachedPhoto = new() { Expiration = PhotoLifetime, LocalCacheExpiration = PhotoLifetime };

    private static readonly HybridCacheEntryOptions CachedPhotoOnly = new() { Flags = HybridCacheEntryFlags.DisableUnderlyingData };

    public async Task<Result<DirectorySearch>> SearchAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        using var request = new HttpRequestMessage(HttpMethod.Get, SearchPath(text));
        request.Headers.Add(MicrosoftGraph.ConsistencyLevelHeader, MicrosoftGraph.EventualConsistency);
        var sent = await SendAsync(request, SearchOperation, DirectoryScopes, SignedInPerson(), HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (sent.IsFailure)
        {
            return sent.Error!;
        }

        using var response = sent.Value;
        if (!response.IsSuccessStatusCode)
        {
            return await RefusalAsync(response, SearchOperation, cancellationToken).ConfigureAwait(false);
        }

        var page = await response.Content.ReadFromJsonAsync<UsersPage>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Microsoft Graph answered a directory search without a body.");

        return new DirectorySearch([.. (page.Value ?? []).Select(ToPerson).OfType<DirectoryPerson>()], page.NextLink is not null);
    }

    public async Task<Result<DirectoryPerson>> FindAsync(Guid objectId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"users/{objectId:D}?$select={SelectedProperties}");
        var sent = await SendAsync(request, FindOperation, DirectoryScopes, SignedInPerson(), HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (sent.IsFailure)
        {
            return sent.Error!;
        }

        using var response = sent.Value;
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return DirectoryErrors.PersonNotFound;
        }

        if (!response.IsSuccessStatusCode)
        {
            return await RefusalAsync(response, FindOperation, cancellationToken).ConfigureAwait(false);
        }

        var user = await response.Content.ReadFromJsonAsync<GraphUser>(Json, cancellationToken).ConfigureAwait(false);

        return ToPerson(user) is { } person && person.ObjectId == objectId
            ? person
            : throw new InvalidOperationException("Microsoft Graph answered the lookup of a person with another person or with one that has no sign-in name.");
    }

    // The cache keeps the photo's bytes, and an empty array for a person without one; a read that finds no entry answers
    // null, because it never asks Graph itself.
    public async Task<Result<DirectoryPhoto>> OwnPhotoAsync(CancellationToken cancellationToken)
    {
        var person = SignedInPerson();
        var key = PhotoCacheKey(person);
        var cached = await cache.GetOrCreateAsync(key, static _ => ValueTask.FromResult<byte[]?>(null), CachedPhotoOnly, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached.Length == 0 ? DirectoryErrors.PhotoNotFound : new DirectoryPhoto(cached, CachedContentTypeOf(cached));
        }

        var photo = await ReadOwnPhotoAsync(person, cancellationToken).ConfigureAwait(false);
        if (photo.IsSuccess || photo.Error == DirectoryErrors.PhotoNotFound)
        {
            await cache.SetAsync(key, photo.IsSuccess ? photo.Value.Content : [], CachedPhoto, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return photo;
    }

    internal static string SearchPath(string text)
    {
        var term = text.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        var search = string.Join(" OR ", SearchedProperties.Select(property => $"\"{property}:{term}\""));

        return $"users?$search={Uri.EscapeDataString(search)}&$select={SelectedProperties}&$orderby=displayName&$top={DirectorySearch.MaxPeople}&$count=true";
    }

    internal static string PhotoCacheKey(ClaimsPrincipal person) =>
        SignInAudit.ObjectIdOf(person) is { } objectId
            ? $"{PhotoCacheKeyPrefix}{objectId:D}"
            : throw new InvalidOperationException("The signed-in person carries no Entra object id, by which the directory keeps their photo.");

    // Every user of the directory has an object id and a user principal name; one Graph returned without either could not be
    // recorded, so it is left out rather than offered. A guest of another organisation is held under a user principal name
    // that quotes, before #EXT#, the address it was invited with (Microsoft Entra External ID, "Properties of a B2B guest
    // user"); Graph reports that address as its mail, and the guest signs in with it, so it is the guest's sign-in name.
    private static DirectoryPerson? ToPerson(GraphUser? user)
    {
        if (user is not { UserPrincipalName: { Length: > 0 } userPrincipalName } || !Guid.TryParse(user.Id, out var objectId))
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? userPrincipalName : user.DisplayName.Trim();
        var mail = string.IsNullOrWhiteSpace(user.Mail) ? null : user.Mail.Trim();
        var signInName = mail is not null && userPrincipalName.Contains(GuestMarker, StringComparison.OrdinalIgnoreCase) ? mail : userPrincipalName;

        return new DirectoryPerson(objectId, displayName, signInName, userPrincipalName, mail);
    }

    private static string CachedContentTypeOf(byte[] photo) =>
        ProfilePhotos.ContentTypeOf(photo) ?? throw new InvalidOperationException("The cache holds a profile photo that is no JPEG, PNG, GIF or WebP image.");

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

    private ClaimsPrincipal SignedInPerson() =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user
            : throw new InvalidOperationException("The company directory is read only on behalf of a signed-in person.");

    private async Task<Result<DirectoryPhoto>> ReadOwnPhotoAsync(ClaimsPrincipal person, CancellationToken cancellationToken)
    {
        foreach (var path in PhotoPaths)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            var sent = await SendAsync(request, PhotoOperation, OwnProfileScopes, person, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (sent.IsFailure)
            {
                return sent.Error!;
            }

            using var response = sent.Value;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            return response.IsSuccessStatusCode
                ? await PhotoOfAsync(response, cancellationToken).ConfigureAwait(false)
                : await RefusalAsync(response, PhotoOperation, cancellationToken).ConfigureAwait(false);
        }

        return DirectoryErrors.PhotoNotFound;
    }

    private async Task<Result<DirectoryPhoto>> PhotoOfAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        byte[]? content;
        try
        {
            content = await ProfilePhotos.ReadAsync(response.Content, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException && !cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, exception, PhotoOperation);
            return DirectoryErrors.Unavailable;
        }

        if (content is null)
        {
            throw new InvalidOperationException($"Microsoft Graph answered a profile photo larger than {ProfilePhotos.MaxBytes} bytes.");
        }

        return ProfilePhotos.ContentTypeOf(content) is { } contentType
            ? new DirectoryPhoto(content, contentType)
            : throw new InvalidOperationException("Microsoft Graph answered a profile photo that is no JPEG, PNG, GIF or WebP image.");
    }

    private async Task<Result<HttpResponseMessage>> SendAsync(
        HttpRequestMessage request,
        string operation,
        IEnumerable<string> scopes,
        ClaimsPrincipal person,
        HttpCompletionOption completion,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await authorizationHeaders.CreateAuthorizationHeaderForUserAsync(scopes, TokenOptions, person, cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
        }
        catch (MicrosoftIdentityWebChallengeUserException exception)
        {
            LogTokenRefused(logger, operation, exception.MsalUiRequiredException.ErrorCode, exception.MsalUiRequiredException.Classification);
            return DirectoryErrors.AccessDenied;
        }
        catch (MsalUiRequiredException exception)
        {
            LogTokenRefused(logger, operation, exception.ErrorCode, exception.Classification);
            return DirectoryErrors.AccessDenied;
        }
        catch (MsalException exception)
        {
            LogTokenUnavailable(logger, operation, exception.ErrorCode);
            return DirectoryErrors.Unavailable;
        }

        try
        {
            return await client.SendAsync(request, completion, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            LogUnreachable(logger, exception, operation);
            return DirectoryErrors.Unavailable;
        }
        catch (ExecutionRejectedException exception)
        {
            LogUnreachable(logger, exception, operation);
            return DirectoryErrors.Unavailable;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, exception, operation);
            return DirectoryErrors.Unavailable;
        }
    }

    private async Task<Error> RefusalAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var code = await GraphErrorCodeAsync(response, cancellationToken).ConfigureAwait(false);
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                LogRefused(logger, operation, status, code);
                return DirectoryErrors.AccessDenied;
            case HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError:
                LogUnavailable(logger, operation, status, code);
                return DirectoryErrors.Unavailable;
            default:
                throw new InvalidOperationException($"Microsoft Graph answered the directory {operation} with {status} {code}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Entra refused the token of the directory {Operation}: {ErrorCode} ({Classification})")]
    private static partial void LogTokenRefused(ILogger logger, string operation, string errorCode, UiRequiredExceptionClassification classification);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Entra could not issue the token of the directory {Operation}: {ErrorCode}")]
    private static partial void LogTokenUnavailable(ILogger logger, string operation, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Graph refused the directory {Operation} with {StatusCode} {GraphErrorCode}")]
    private static partial void LogRefused(ILogger logger, string operation, int statusCode, string? graphErrorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Graph could not answer the directory {Operation}: {StatusCode} {GraphErrorCode}")]
    private static partial void LogUnavailable(ILogger logger, string operation, int statusCode, string? graphErrorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Microsoft Graph could not be reached for the directory {Operation}")]
    private static partial void LogUnreachable(ILogger logger, Exception exception, string operation);

    private sealed record UsersPage(IReadOnlyList<GraphUser>? Value, [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record GraphUser(string? Id, string? DisplayName, string? Mail, string? UserPrincipalName);

    private sealed record GraphErrorBody(GraphError? Error);

    private sealed record GraphError(string? Code);
}
