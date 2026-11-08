using System.Net;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;
using Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Responses;
using Dewiride.Erp.Testing.Numbering;

namespace Dewiride.Erp.Modules.Platform.Numbering.IntegrationTests.Series.Endpoints;

// Each test that sets a numbering up takes a test kind of document no other test of the process takes.
public sealed class SeriesEndpointsTests(NumberingHost host) : IClassFixture<NumberingHost>
{
    private static readonly object Format = NumberingApi.Format("TI/", "abbreviated", "/", 3);

    private FinancialYear CurrentYear => FinancialYear.Current(host.Factory.Services.GetRequiredService<TimeProvider>());

    [Fact]
    public async Task List_TheDeclaredKinds_ByTitleWithTheYearsRuleFromTheParameterStore()
    {
        using var client = host.Administrator();
        var document = TestNumberedDocuments.Numbered(1);

        var listed = await NumberingApi.ListAsync(client);

        Assert.Equal(TestNumberedDocuments.All.Select(d => d.Title).Order(StringComparer.Ordinal), listed.Select(d => d.Title));
        var first = Assert.Single(listed, d => d.DocumentType == document.Key);
        var rule = await NumberingApi.RuleOfAsync(host.Factory.Services, document, CurrentYear);
        Assert.Equal((document.Description, CurrentYear.Label, new SerialNumberRuleResponse(rule.MaxLength, rule.SpecialCharacters)), (first.Description, first.CurrentYear, first.Rule));
    }

    [Fact]
    public async Task SetUp_AFormatTheRuleAllows_CreatesTheNumberingWithTheCurrentYearOpen()
    {
        using var client = host.Administrator();
        var document = TestNumberedDocuments.Numbered(2);
        var year = CurrentYear;
        var shortYear = year.Label[2..];

        using var response = await NumberingApi.SetUpAsync(client, document, Format, 38);

        var created = await NumberingApi.ReadAsync<NumberedDocumentResponse>(response, HttpStatusCode.Created);
        var series = created.Series!;
        Assert.Equal(new NumberFormatResponse("TI/", NumberYearStyle.Abbreviated, "/", 3), series.Format);
        var opened = Assert.Single(series.Years);
        Assert.Equal((year.Label, 38L, 38L, false, $"TI/{shortYear}/038", (string?)null), (opened.Year, opened.FirstNumber, opened.NextNumber, opened.InUse, opened.FirstText, opened.LastIssuedText));
        Assert.Equal(NumberingApi.SeriesPath(series.Id).OriginalString, response.Headers.Location?.OriginalString);
        using var read = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(series.Id, (await NumberingApi.ReadAsync<NumberedDocumentResponse>(read, HttpStatusCode.OK)).Series!.Id);
    }

    [Fact]
    public async Task SetUp_TheSameIdempotencyKeyTwice_SetsUpOnceAndReplaysTheAnswer()
    {
        using var client = host.Administrator();
        var document = TestNumberedDocuments.Numbered(3);
        var key = Guid.CreateVersion7().ToString();

        using var first = await NumberingApi.SetUpAsync(client, document, Format, 1, key);
        using var replay = await NumberingApi.SetUpAsync(client, document, Format, 1, key);

        var created = await NumberingApi.ReadAsync<NumberedDocumentResponse>(first, HttpStatusCode.Created);
        Assert.Equal(created.Series!.Id, (await NumberingApi.ReadAsync<NumberedDocumentResponse>(replay, HttpStatusCode.Created)).Series!.Id);
        using var again = await NumberingApi.SetUpAsync(client, document, Format, 1);
        await NumberingApi.AssertProblemAsync(again, HttpStatusCode.Conflict, "numbering.already-set-up");
    }

    [Fact]
    public async Task SetUp_AFormatTheRuleRefuses_AnswersAValidationProblemOfTheField()
    {
        using var client = host.Administrator();
        var document = TestNumberedDocuments.Numbered(4);
        var rule = await NumberingApi.RuleOfAsync(host.Factory.Services, document, CurrentYear);

        using var lowerCase = await NumberingApi.SetUpAsync(client, document, NumberingApi.Format("ti/", "full", "/", 3), 1);
        using var tooLong = await NumberingApi.SetUpAsync(client, document, NumberingApi.Format(new string('A', rule.MaxLength), "none", "", 1), 1);

        var lowerProblem = await NumberingApi.AssertProblemAsync(lowerCase, HttpStatusCode.BadRequest, "numbering.format-invalid");
        Assert.Equal(["format.prefix"], lowerProblem.Errors.Keys);
        var longProblem = await NumberingApi.AssertProblemAsync(tooLong, HttpStatusCode.BadRequest, "numbering.format-invalid");
        Assert.Equal(["format"], longProblem.Errors.Keys);
        Assert.Null((await NumberingApi.DocumentAsync(client, document)).Series);
    }

    [Fact]
    public async Task SetUp_ARequestWithoutAFormatOrNextNumber_IsRefusedByTheRequestsValidation()
    {
        using var client = host.Administrator();
        using var request = new HttpRequestMessage(HttpMethod.Post, NumberingApi.SeriesPath()) { Content = JsonContent.Create(new { documentType = TestNumberedDocuments.Numbered(5).Key }) };
        request.Headers.Add(NumberingApi.IdempotencyKeyHeader, Guid.CreateVersion7().ToString());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var problem = await NumberingApi.AssertProblemAsync(response, HttpStatusCode.BadRequest, "request.invalid");
        Assert.Contains("format", problem.Errors.Keys);
        Assert.Contains("nextNumber", problem.Errors.Keys);
    }

    [Fact]
    public async Task Change_BeforeTheYearGivesANumber_ReformatsTheYear()
    {
        using var client = host.Administrator();
        var document = TestNumberedDocuments.Numbered(6);
        var series = await SetUpAsync(client, document);

        using var response = await NumberingApi.ChangeAsync(client, series.Id, NumberingApi.Format("TX-", "none", "", 5), 7, series.Version);

        var changed = await NumberingApi.ReadAsync<NumberedDocumentResponse>(response, HttpStatusCode.OK);
        Assert.Equal("TX-00007", Assert.Single(changed.Series!.Years).FirstText);
    }

    [Fact]
    public async Task Change_AYearThatGaveNumbers_KeepsItsNextNumberAndAppliesTheFormatFromTheNextYear()
    {
        using var client = host.Administrator();
        var document = TestNumberedDocuments.Numbered(7);
        var series = await SetUpAsync(client, document);
        var issued = await NumberIssuer.IssueAsync(host.Factory.Services, document.Key, 2, TestContext.Current.CancellationToken);
        Assert.True(issued.IsSuccess, issued.Error?.Message);
        var shortYear = CurrentYear.Label[2..];

        using var refused = await NumberingApi.ChangeAsync(client, series.Id, Format, 1, series.Version);
        using var accepted = await NumberingApi.ChangeAsync(client, series.Id, NumberingApi.Format("TX-", "none", "", 5), 3, series.Version);

        var problem = await NumberingApi.AssertProblemAsync(refused, HttpStatusCode.BadRequest, "numbering.year-in-use");
        Assert.Equal(["nextNumber"], problem.Errors.Keys);
        var changed = await NumberingApi.ReadAsync<NumberedDocumentResponse>(accepted, HttpStatusCode.OK);
        Assert.Equal(new NumberFormatResponse("TX-", NumberYearStyle.None, "", 5), changed.Series!.Format);
        var year = Assert.Single(changed.Series.Years);
        Assert.Equal((true, $"TI/{shortYear}/002", $"TI/{shortYear}/003"), (year.InUse, year.LastIssuedText, year.NextText));
    }

    [Fact]
    public async Task Change_FromAnOutdatedOrMalformedVersion_IsRefused()
    {
        using var client = host.Administrator();
        var document = TestNumberedDocuments.Numbered(8);
        var series = await SetUpAsync(client, document);
        using var first = await NumberingApi.ChangeAsync(client, series.Id, NumberingApi.Format("TX/", "full", "/", 2), null, series.Version);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var stale = await NumberingApi.ChangeAsync(client, series.Id, Format, null, series.Version);
        using var malformed = await NumberingApi.ChangeAsync(client, series.Id, Format, null, "not-a-version");

        await NumberingApi.AssertProblemAsync(stale, HttpStatusCode.Conflict, "concurrency.conflict");
        await NumberingApi.AssertProblemAsync(malformed, HttpStatusCode.BadRequest, "numbering.version-invalid");
    }

    [Fact]
    public async Task ChangeAndGet_AnIdNoNumberingHas_AreNotFound()
    {
        using var client = host.Administrator();

        using var change = await NumberingApi.ChangeAsync(client, Guid.CreateVersion7(), Format, null, Convert.ToBase64String(new byte[8]));
        using var read = await client.GetAsync(NumberingApi.SeriesPath(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        await NumberingApi.AssertProblemAsync(change, HttpStatusCode.NotFound, "numbering.series-not-found");
        await NumberingApi.AssertProblemAsync(read, HttpStatusCode.NotFound, "numbering.series-not-found");
    }

    private static async Task<NumberSeriesResponse> SetUpAsync(HttpClient client, NumberedDocument document)
    {
        using var response = await NumberingApi.SetUpAsync(client, document, Format, 1);

        return (await NumberingApi.ReadAsync<NumberedDocumentResponse>(response, HttpStatusCode.Created)).Series!;
    }
}
