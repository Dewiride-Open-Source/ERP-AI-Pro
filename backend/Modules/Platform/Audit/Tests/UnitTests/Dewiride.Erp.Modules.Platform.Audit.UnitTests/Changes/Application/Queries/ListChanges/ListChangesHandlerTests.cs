using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Platform.Audit.Changes.Application.Queries.ListChanges;

namespace Dewiride.Erp.Modules.Platform.Audit.UnitTests.Changes.Application.Queries.ListChanges;

public sealed class ListChangesHandlerTests
{
    [Fact]
    public async Task HandleAsync_Request_HandsItToTheHistoryAndReturnsItsPage()
    {
        var change = new ChangeRecordDetails(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 10, 16, 9, 30, 0, TimeSpan.Zero),
            ChangeOperation.Deleted,
            "identity.users.designations",
            Guid.CreateVersion7(),
            "identity.users.designations",
            Guid.CreateVersion7(),
            "Analyst",
            ChangeActorKind.System,
            ActorIds.System,
            null,
            null,
            null,
            []);
        var page = new PagedResult<ChangeRecordDetails>([change], 2, 20, 21);
        var history = new RecordingChangeHistory { Page = page };
        var request = ListRequest.Parse(2, 20, "occurredAt:asc", "operation:eq:deleted").Value;

        var result = await new ListChangesHandler(history).HandleAsync(new ListChangesQuery(request), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.Value);
        Assert.Same(request, history.ReceivedRequest);
        Assert.Equal(TestContext.Current.CancellationToken, history.ReceivedCancellation);
    }

    [Fact]
    public async Task HandleAsync_RequestTheHistoryRefuses_ReturnsTheRefusal()
    {
        var refusal = QueryErrors.Field("changes", ["occurredAt"]);
        var history = new RecordingChangeHistory { Page = refusal };

        var result = await new ListChangesHandler(history).HandleAsync(new ListChangesQuery(ListRequest.Default), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(refusal, result.Error);
    }
}
