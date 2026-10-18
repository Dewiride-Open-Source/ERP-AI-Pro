using System.Net;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Paging;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;

// Every host of the test process records its changes in the one test database, so a test reads the changes filed under a
// record it made itself, oldest first, through the history's route as an administrator.
internal static class ChangeRecordsApi
{
    public const string PeopleKey = "identity.users.people";

    public const string DepartmentsKey = "identity.users.departments";

    public const string DesignationsKey = "identity.users.designations";

    public const string PositionsKey = "identity.users.positions";

    public const string ThemesKey = "identity.users.themes";

    private const string Route = "/api/platform/audit/changes";

    public static async Task<IReadOnlyList<RecordedChange>> OfAsync(HttpClient administrator, Guid subjectId)
    {
        var filter = Uri.EscapeDataString($"subjectId:eq:{subjectId}");
        using var response = await administrator.GetAsync(new Uri($"{Route}?sort=occurredAt:asc&pageSize=200&filter={filter}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<RecordedChange>>(PeopleApi.Json, TestContext.Current.CancellationToken);
        Assert.NotNull(page);

        return page.Items;
    }

    public static string CorrelationIdOf(HttpResponseMessage response) => Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
}
