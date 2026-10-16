using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Application.Commands.ExportChanges;

namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Application.Commands.ExportChanges;

public sealed class ExportChangesHandlerTests
{
    private const string ClientAddress = "198.51.100.23";

    private const string CorrelationId = "0af7651916cd43dd8448eb211c80319c";

    private static readonly Guid AdministratorObjectId = Guid.Parse("8b6e1f47-2c9a-4d3b-b5e0-7a19c4d2e865");

    private static readonly TestRequestOrigin Origin = new(ClientAddress, CorrelationId);

    [Fact]
    public async Task HandleAsync_ExportWithinTheCap_RecordsTheExportOfTheAdministratorAndReturnsIt()
    {
        var history = new RecordingChangeHistory();
        var recorder = new RecordingSecurityEventRecorder();
        var handler = new ExportChangesHandler(history, recorder, new TestActorContext(AdministratorObjectId, IsAuthenticated: true), Origin);
        var filter = FilterRequest.Parse("entityType:eq:identity.users.people").Value;

        var result = await handler.HandleAsync(new ExportChangesCommand(filter), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(RecordingChangeHistory.SampleExport, result.Value);
        Assert.Same(filter, history.ReceivedFilter);
        Assert.Equal(TestContext.Current.CancellationToken, history.ReceivedCancellation);
        Assert.Equal(
            new SecurityEventEntry(SecurityEventKind.ChangeHistoryExported, "records:3", AdministratorObjectId, null, ClientAddress, CorrelationId),
            Assert.Single(recorder.Entries));
    }

    [Fact]
    public async Task HandleAsync_ExportTheHistoryRefuses_ReturnsTheRefusalAndRecordsNothing()
    {
        var recorder = new RecordingSecurityEventRecorder();
        var history = new RecordingChangeHistory { Export = ChangeHistoryErrors.ExportTooLarge };
        var handler = new ExportChangesHandler(history, recorder, new TestActorContext(AdministratorObjectId, IsAuthenticated: true), Origin);

        var result = await handler.HandleAsync(new ExportChangesCommand(FilterRequest.None), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ChangeHistoryErrors.ExportTooLarge, result.Error);
        Assert.Empty(recorder.Entries);
    }

    [Fact]
    public async Task HandleAsync_ExportWhoseRecordCannotBeWritten_ThrowsInsteadOfReturningTheExport()
    {
        var failure = new TimeoutException("The audit database did not answer.");
        var recorder = new RecordingSecurityEventRecorder { Failure = failure };
        var handler = new ExportChangesHandler(new RecordingChangeHistory(), recorder, new TestActorContext(AdministratorObjectId, IsAuthenticated: true), Origin);

        var thrown = await Assert.ThrowsAsync<TimeoutException>(() => handler.HandleAsync(new ExportChangesCommand(FilterRequest.None), TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task HandleAsync_CallerNotSignedIn_RecordsTheExportWithoutAnActor()
    {
        var recorder = new RecordingSecurityEventRecorder();
        var handler = new ExportChangesHandler(new RecordingChangeHistory(), recorder, new TestActorContext(ActorIds.Anonymous, IsAuthenticated: false), Origin);

        var result = await handler.HandleAsync(new ExportChangesCommand(FilterRequest.None), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(Assert.Single(recorder.Entries).ActorObjectId);
    }
}
