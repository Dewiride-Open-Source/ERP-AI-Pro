using System.Net;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints.Export;

public sealed class ExportRefusalTests
{
    [Fact]
    public async Task Get_ExportTheHistoryFindsTooLarge_AnswersUnprocessableWithoutTheFileAndRecordsNothing()
    {
        using var root = new ErpApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IChangeHistory>(new StubChangeHistory(ChangeHistoryErrors.ExportTooLarge))));
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(ChangesApi.ExportPath("operation:eq:created"), TestContext.Current.CancellationToken);

        var problem = await ChangesApi.AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, ChangeHistoryErrors.ExportTooLarge.Code);
        Assert.Equal(ChangeHistoryErrors.ExportTooLarge.Message, problem.Detail);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await AuditTables.SecurityEventsOfAsync(response));
    }

    [Fact]
    public async Task Get_ExportWhoseRecordCannotBeWritten_AnswersServerErrorWithoutTheFile()
    {
        using var root = new ErpApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddScoped<ISecurityEventRecorder, RefusingSecurityEventRecorder>()));
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);

        using var response = await client.GetAsync(ChangesApi.ExportPath($"subjectId:eq:{Guid.CreateVersion7()}"), TestContext.Current.CancellationToken);

        await ChangesApi.AssertProblemAsync(response, HttpStatusCode.InternalServerError, "server.error");
        Assert.Null(response.Content.Headers.ContentDisposition);
    }
}
