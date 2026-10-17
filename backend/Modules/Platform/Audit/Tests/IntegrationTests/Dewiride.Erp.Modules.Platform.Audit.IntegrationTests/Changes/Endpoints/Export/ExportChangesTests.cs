using System.Globalization;
using System.Net;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints.Export;

public sealed class ExportChangesTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private const string FetchSiteHeader = "Sec-Fetch-Site";

    private static readonly string[] Header =
    [
        "Time (IST)",
        "Time (UTC)",
        "Action",
        "Subject type",
        "Subject id",
        "Subject",
        "Record type",
        "Record id",
        "Changed by",
        "Changed by id",
        "Client address",
        "Correlation id",
        "Field",
        "Before",
        "Before name",
        "After",
        "After name",
    ];

    [Fact]
    public async Task Get_ExportOfADepartment_StreamsItsChangesOldestFirstAsAFileWithOneLinePerValue()
    {
        using var client = Administrator();
        var token = Guid.CreateVersion7().ToString("N");
        var department = await ChangesApi.CreateDepartmentAsync(client, $"=Payroll {token}");
        await ChangesApi.UpdateDepartmentAsync(client, department, $"Payroll {token}", parentId: null);
        await ChangesApi.DeleteDepartmentAsync(client, department.Id);

        using var response = await client.GetAsync(ChangesApi.ExportPath($"subjectId:eq:{department.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.False(response.Content.Headers.NonValidated.Contains("Content-Length"));
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal<byte>([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = CsvText.Parse(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Equal(Header, lines[0]);
        Assert.Equal(
            [
                ("Created", "name", "", $"'=Payroll {token}"),
                ("Created", "parentId", "", ""),
                ("Updated", "name", $"'=Payroll {token}", $"Payroll {token}"),
                ("Deleted", "", "", ""),
            ],
            lines.Skip(1).Select(cells => (cells[2], cells[12], cells[13], cells[15])));
        Assert.All(lines.Skip(1), cells =>
        {
            Assert.Equal(ChangesApi.DepartmentKey, cells[3]);
            Assert.Equal(department.Id.ToString("D"), cells[4]);
            Assert.Equal($"Payroll {token}", cells[5]);
            Assert.Equal(ChangesApi.DepartmentKey, cells[6]);
            Assert.Equal(department.Id.ToString("D"), cells[7]);
            Assert.Equal(TestUsers.Administrator.ObjectId.ToString("D"), cells[9]);
        });
    }

    [Fact]
    public async Task Get_ExportOfNoChanges_NamesTheFileAfterTheIndianTimeTheExportStartedAndHoldsTheHeaderLine()
    {
        using var client = Administrator();
        var before = TimeProvider.System.GetUtcNow();

        using var response = await client.GetAsync(ChangesApi.ExportPath($"subjectId:eq:{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        var after = TimeProvider.System.GetUtcNow();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.NotNull(disposition.FileNameStar);
        Assert.Matches(@"^change-history-\d{8}-\d{6}\.csv$", disposition.FileNameStar);
        Assert.Equal(disposition.FileNameStar, disposition.FileName?.Trim('"'));
        var started = new DateTimeOffset(
            DateTime.ParseExact(disposition.FileNameStar["change-history-".Length..^".csv".Length], "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
            IndianStandardTime.Offset);
        Assert.InRange(started, before.AddSeconds(-1), after);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Header, Assert.Single(CsvText.Parse(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3))));
    }

    [Fact]
    public async Task Get_Export_RecordsTheExportAsASecurityEventOfTheAdministrator()
    {
        using var client = Administrator();
        var department = await ChangesApi.CreateDepartmentAsync(client, ChangesApi.UniqueName("Audit"));
        await ChangesApi.DeleteDepartmentAsync(client, department.Id);

        using var response = await client.GetAsync(ChangesApi.ExportPath($"subjectId:eq:{department.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([(SecurityEventKind.ChangeHistoryExported, "records:2", TestUsers.Administrator.ObjectId)], await AuditTables.SecurityEventsOfAsync(response));
    }

    [Theory]
    [InlineData("subjectId", "query.invalid-filter")]
    [InlineData("name:eq:Payroll", "query.invalid-field")]
    [InlineData("entityId:contains:0199", "query.invalid-operator")]
    [InlineData("occurredAt:gt:yesterday", "query.invalid-value")]
    public async Task Get_ExportFilterOutsideTheGrammarOrFieldsOfTheList_AnswersTheQueryErrorCodeAndRecordsNothing(string filter, string code)
    {
        using var client = Administrator();

        using var response = await client.GetAsync(ChangesApi.ExportPath(filter), TestContext.Current.CancellationToken);

        await ChangesApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
        Assert.Empty(await AuditTables.SecurityEventsOfAsync(response));
    }

    [Fact]
    public async Task Get_ExportAsPersonWithoutThePermission_AnswersForbiddenAndRecordsNothing()
    {
        using var client = factory.CreateClient().AsUser(TestUsers.Accountant);

        using var response = await client.GetAsync(ChangesApi.ExportPath($"subjectId:eq:{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        await ChangesApi.AssertProblemAsync(response, HttpStatusCode.Forbidden, "request.forbidden");
        Assert.Empty(await AuditTables.SecurityEventsOfAsync(response));
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    [InlineData("none")]
    public async Task Get_ExportSentByTheBrowserForAnotherSiteOrTheAddressBar_AnswersInvalidWithoutTheFileAndRecordsNothing(string fetchSite)
    {
        using var client = Administrator();
        using var request = ExportRequest(fetchSite);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await ChangesApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "request.invalid");
        Assert.Equal(ChangeEndpoints.NotThisSiteMessage, problem.Detail);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await AuditTables.SecurityEventsOfAsync(response));
    }

    [Theory]
    [InlineData("same-origin")]
    [InlineData(null)]
    public async Task Get_ExportSentByAScriptOfThisSiteOrByNoBrowser_StreamsTheFileAndRecordsTheExport(string? fetchSite)
    {
        using var client = Administrator();
        using var request = ExportRequest(fetchSite);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal([(SecurityEventKind.ChangeHistoryExported, "records:0", TestUsers.Administrator.ObjectId)], await AuditTables.SecurityEventsOfAsync(response));
    }

    [Fact]
    public void ExportEndpoint_Metadata_RunsUnderItsOwnTimeoutAndTheExportConcurrencyPolicy()
    {
        var export = EndpointNamed(ChangeEndpoints.ExportRouteName);

        Assert.Equal(TimeSpan.FromMinutes(5), export.Metadata.GetMetadata<RequestTimeoutPolicy>()?.Timeout);
        Assert.Equal(ChangeEndpoints.ExportConcurrencyPolicy, export.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public void ListEndpoint_Metadata_KeepsTheDefaultTimeoutAndNoExportCap()
    {
        var list = EndpointNamed(ChangeEndpoints.ListRouteName);

        Assert.Null(list.Metadata.GetMetadata<RequestTimeoutPolicy>());
        Assert.Null(list.Metadata.GetMetadata<RequestTimeoutAttribute>());
        Assert.Null(list.Metadata.GetMetadata<EnableRateLimitingAttribute>());
    }

    private static HttpRequestMessage ExportRequest(string? fetchSite)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ChangesApi.ExportPath($"subjectId:eq:{Guid.CreateVersion7()}"));
        if (fetchSite is not null)
        {
            request.Headers.Add(FetchSiteHeader, fetchSite);
        }

        return request;
    }

    private HttpClient Administrator() => factory.CreateClient().AsUser(TestUsers.Administrator);

    private RouteEndpoint EndpointNamed(string routeName) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == routeName);
}
