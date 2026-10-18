using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Authentication.Graph;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Dewiride.Erp.Testing.Graph;

// Every test host sends its Microsoft Graph requests here instead of to Graph. The endpoint answers the user search and the
// user lookup of the company directory, for the people a test adds, the way Graph does, and only to a bearer token the test
// token endpoint issued for the directory permission; a search also needs ConsistencyLevel eventual. A display name matches
// when every word of the search text starts one of its words, an email address or a sign-in name when it starts with the
// text, and a person added with a deleted account is found by neither. It answers the photo of the person a token was issued
// to, only to a token issued for User.Read, and ImageNotFound for a person without one or outside the directory. It answers
// the accounts asked by id, at most 15 at a time as Graph allows, only to an application token, refusing a person's token as
// Graph refuses one without the application permission: every account is enabled unless a test added its person with a
// disabled or a deleted account, which is left out of the answer, because every host of the process writes to the one test
// database, so a check one test runs must never deactivate the people of another. A test can make the directory refuse every
// request, or every application token. Anything else fails the request that sent it, so no test host reaches Graph.
public sealed class TestDirectory(TestTokenEndpoint tokenEndpoint)
{
    public const string AccessDeniedCode = "Authorization_RequestDenied";

    public const string ImageNotFoundCode = "ImageNotFound";

    public const string SizedPhotoPath = "/v1.0/me/photos/240x240/$value";

    public const string PhotoPath = "/v1.0/me/photo/$value";

    private const int MaxFilterValues = 15;

    private const string UsersPath = "/v1.0/users";

    private const string FilterParameter = "$filter";

    private const string IdFilterPrefix = "id in (";

    private readonly ConcurrentDictionary<Guid, TestDirectoryPerson> _people = new();

    private readonly ConcurrentQueue<TestDirectoryRequest> _requests = new();

    private volatile bool _refusing;

    private volatile bool _refusingApplications;

    public IReadOnlyCollection<TestDirectoryRequest> Requests => _requests;

    public void Add(TestDirectoryPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        _people[person.ObjectId] = person;
    }

    public void RefuseEveryRequest() => _refusing = true;

    public void RefuseApplicationTokens() => _refusingApplications = true;

    public void AllowApplicationTokens() => _refusingApplications = false;

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var address = request.RequestUri ?? throw new InvalidOperationException("The API sent Microsoft Graph a request without an address.");
        var photo = address.AbsolutePath is SizedPhotoPath or PhotoPath;
        if (request.Method != HttpMethod.Get || address.Host != MicrosoftGraph.BaseAddress.Host || !(photo || address.AbsolutePath.StartsWith(UsersPath, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"The API sent {request.Method} {address.GetLeftPart(UriPartial.Path)} to Microsoft Graph, which the test directory does not serve; no test host may reach Graph.");
        }

        var consistencyLevel = request.Headers.TryGetValues(MicrosoftGraph.ConsistencyLevelHeader, out var levels) ? string.Join(',', levels) : null;
        var accessToken = request.Headers.Authorization is { Scheme: "Bearer", Parameter: { } parameter } ? parameter : null;
        _requests.Enqueue(new TestDirectoryRequest(address, consistencyLevel, accessToken));

        var query = QueryHelpers.ParseQuery(address.Query);
        if (address.AbsolutePath == UsersPath && query.ContainsKey(FilterParameter))
        {
            return Accounts(query, accessToken);
        }

        var permission = photo ? MicrosoftGraph.ReadOwnProfileScope : MicrosoftGraph.ReadBasicProfilesScope;
        if (accessToken is null || tokenEndpoint.ScopeOf(accessToken) is not { } scope || !Grants(scope, permission))
        {
            return Error(HttpStatusCode.Unauthorized, "InvalidAuthenticationToken");
        }

        if (_refusing)
        {
            return Error(HttpStatusCode.Forbidden, AccessDeniedCode);
        }

        if (photo)
        {
            return OwnPhoto(tokenEndpoint.SubjectOf(accessToken), sized: address.AbsolutePath == SizedPhotoPath);
        }

        if (address.AbsolutePath == UsersPath)
        {
            return Search(address, consistencyLevel);
        }

        if (address.AbsolutePath[UsersPath.Length] == '/' && Guid.TryParseExact(address.AbsolutePath[(UsersPath.Length + 1)..], "D", out var objectId))
        {
            return Lookup(objectId);
        }

        throw new InvalidOperationException($"The API sent GET {address.GetLeftPart(UriPartial.Path)} to Microsoft Graph, which the test directory does not serve; no test host may reach Graph.");
    }

    private static bool TryParseIds(string filter, out List<Guid> ids)
    {
        ids = [];
        if (!filter.StartsWith(IdFilterPrefix, StringComparison.Ordinal) || !filter.EndsWith(')'))
        {
            return false;
        }

        foreach (var item in filter[IdFilterPrefix.Length..^1].Split(','))
        {
            var value = item.Trim();
            if (value.Length < 2 || value[0] != '\'' || value[^1] != '\'' || !Guid.TryParseExact(value[1..^1], "D", out var id))
            {
                return false;
            }

            ids.Add(id);
        }

        return ids.Count <= MaxFilterValues;
    }

    // Graph reads the permissions of a token by their short names, which Entra writes into it whichever way the API asked.
    private static bool Grants(string scope, string permission)
    {
        var name = permission[(permission.LastIndexOf('/') + 1)..];

        return scope.Split(' ').Any(granted => string.Equals(granted, permission, StringComparison.Ordinal) || string.Equals(granted, name, StringComparison.Ordinal));
    }

    private HttpResponseMessage Lookup(Guid objectId) =>
        _people.TryGetValue(objectId, out var person) && person.AccountState != DirectoryAccountState.Deleted
            ? Json(HttpStatusCode.OK, ToUser(person))
            : Error(HttpStatusCode.NotFound, "Request_ResourceNotFound");

    private HttpResponseMessage Accounts(Dictionary<string, StringValues> query, string? accessToken)
    {
        if (accessToken is null || tokenEndpoint.ScopeOf(accessToken) is null)
        {
            return Error(HttpStatusCode.Unauthorized, "InvalidAuthenticationToken");
        }

        if (!tokenEndpoint.IsApplicationToken(accessToken) || _refusing || _refusingApplications)
        {
            return Error(HttpStatusCode.Forbidden, AccessDeniedCode);
        }

        if (!TryParseIds(query[FilterParameter].ToString(), out var ids))
        {
            return Error(HttpStatusCode.BadRequest, "Request_UnsupportedQuery");
        }

        var selected = query["$select"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var accounts = new List<Dictionary<string, object?>>();
        foreach (var id in ids)
        {
            var state = _people.TryGetValue(id, out var person) ? person.AccountState : DirectoryAccountState.Enabled;
            if (state == DirectoryAccountState.Deleted)
            {
                continue;
            }

            var account = new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = id.ToString("D") };
            if (selected.Contains("accountEnabled", StringComparer.Ordinal))
            {
                account["accountEnabled"] = state == DirectoryAccountState.Enabled;
            }

            accounts.Add(account);
        }

        return Json(HttpStatusCode.OK, new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = accounts });
    }

    private HttpResponseMessage OwnPhoto(Guid? owner, bool sized)
    {
        if (owner is not { } objectId || !_people.TryGetValue(objectId, out var person) || person.Photo is not { } photo || (sized && !photo.Sized))
        {
            return Error(HttpStatusCode.NotFound, ImageNotFoundCode);
        }

        var content = new ByteArrayContent(photo.Content);
        content.Headers.ContentType = new MediaTypeHeaderValue(photo.ContentType);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private HttpResponseMessage Search(Uri address, string? consistencyLevel)
    {
        if (consistencyLevel != MicrosoftGraph.EventualConsistency)
        {
            return Error(HttpStatusCode.BadRequest, "Request_UnsupportedQuery");
        }

        var query = QueryHelpers.ParseQuery(address.Query);
        var clauses = Clauses(query["$search"].ToString());
        var top = int.Parse(query["$top"].ToString(), CultureInfo.InvariantCulture);
        var matched = _people.Values
            .Where(person => person.AccountState != DirectoryAccountState.Deleted && clauses.Any(clause => Matches(person, clause)))
            .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(person => person.ObjectId)
            .ToList();
        var page = new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = matched.Take(top).Select(ToUser).ToList() };
        if (matched.Count > top)
        {
            page["@odata.nextLink"] = $"{MicrosoftGraph.BaseAddress}users?$skiptoken=next";
        }

        return Json(HttpStatusCode.OK, page);
    }

    // A clause is "property:text" in double quotes, a quote or backslash inside it escaped by a backslash.
    private static List<(string Property, string Text)> Clauses(string search)
    {
        var clauses = new List<(string Property, string Text)>();
        var clause = new StringBuilder();
        var inClause = false;
        var escaped = false;
        foreach (var character in search)
        {
            if (!inClause)
            {
                inClause = character == '"';
            }
            else if (escaped)
            {
                clause.Append(character);
                escaped = false;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else if (character == '"')
            {
                var text = clause.ToString();
                var colon = text.IndexOf(':', StringComparison.Ordinal);
                clauses.Add((text[..colon], text[(colon + 1)..]));
                clause.Clear();
                inClause = false;
            }
            else
            {
                clause.Append(character);
            }
        }

        return clauses;
    }

    private static bool Matches(TestDirectoryPerson person, (string Property, string Text) clause) =>
        clause.Property switch
        {
            "displayName" => clause.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => Words(person.DisplayName).Any(name => name.StartsWith(word, StringComparison.OrdinalIgnoreCase))),
            "mail" => person.Mail is { } mail && mail.StartsWith(clause.Text, StringComparison.OrdinalIgnoreCase),
            "userPrincipalName" => person.UserPrincipalName.StartsWith(clause.Text, StringComparison.OrdinalIgnoreCase),
            _ => throw new InvalidOperationException($"The API searched the directory by {clause.Property}, which the test directory does not serve."),
        };

    private static string[] Words(string displayName) =>
        displayName.Split([.. displayName.Where(character => !char.IsLetterOrDigit(character)).Distinct()], StringSplitOptions.RemoveEmptyEntries);

    private static Dictionary<string, object?> ToUser(TestDirectoryPerson person) => new(StringComparer.Ordinal)
    {
        ["id"] = person.ObjectId.ToString("D"),
        ["displayName"] = person.DisplayName,
        ["mail"] = person.Mail,
        ["userPrincipalName"] = person.UserPrincipalName,
    };

    private static HttpResponseMessage Error(HttpStatusCode status, string code) =>
        Json(status, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["error"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code, ["message"] = $"The test directory answered {code}." },
        });

    private static HttpResponseMessage Json(HttpStatusCode status, Dictionary<string, object?> body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private sealed class Handler(TestDirectory directory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(directory.Answer(request));
    }
}
