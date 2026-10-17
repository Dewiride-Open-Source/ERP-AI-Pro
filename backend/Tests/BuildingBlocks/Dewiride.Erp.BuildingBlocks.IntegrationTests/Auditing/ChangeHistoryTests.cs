using System.Data.SqlTypes;
using System.Globalization;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Auditing.Persistence;
using Dewiride.Erp.BuildingBlocks.IntegrationTests.Persistence.Sample;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Auditing;

// The test database holds the change records of every test class, so each test files its records under subjects of its own and
// filters by them.
public sealed class ChangeHistoryTests(SampleDatabase database) : IClassFixture<SampleDatabase>
{
    private const string PeopleKey = "test.history.people";

    private const string TeamsKey = "test.history.teams";

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);

    private static readonly Guid Asha = new("55555555-5555-5555-5555-555555555555");

    private static readonly Guid Ravi = new("66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task ListAsync_DefaultOrder_ListsNewestFirstAndRecordsOfOneTimeInTheOrderOfTheirIds()
    {
        var subject = Guid.CreateVersion7();
        var first = Record(subject, Now.AddMinutes(-10));
        var tied = new[] { Record(subject, Now.AddMinutes(-5)), Record(subject, Now.AddMinutes(-5)), Record(subject, Now.AddMinutes(-5)) };
        await SaveAsync([first, .. tied]);

        var listed = await ListAsync(filter: $"subjectId:eq:{subject}");

        Assert.Equal([.. tied.Select(r => r.Id.Value).OrderBy(id => new SqlGuid(id)), first.Id.Value], listed.Value.Items.Select(r => r.Id));
        Assert.Equal(4, listed.Value.TotalCount);
    }

    [Fact]
    public async Task ListAsync_SortedByTimeAscending_ListsOldestFirst()
    {
        var subject = Guid.CreateVersion7();
        var records = new[] { Record(subject, Now.AddMinutes(-1)), Record(subject, Now.AddMinutes(-3)), Record(subject, Now.AddMinutes(-2)) };
        await SaveAsync(records);

        var listed = await ListAsync(sort: "occurredAt:asc", filter: $"subjectId:eq:{subject}");

        Assert.Equal([records[1].Id.Value, records[2].Id.Value, records[0].Id.Value], listed.Value.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task ListAsync_SecondPage_ListsTheRecordsAfterTheFirstPage()
    {
        var subject = Guid.CreateVersion7();
        var records = Enumerable.Range(1, 5).Select(minutes => Record(subject, Now.AddMinutes(-minutes))).ToArray();
        await SaveAsync(records);

        var listed = await ListAsync(page: 2, pageSize: 2, filter: $"subjectId:eq:{subject}");

        Assert.Equal([records[2].Id.Value, records[3].Id.Value], listed.Value.Items.Select(r => r.Id));
        Assert.Equal(5, listed.Value.TotalCount);
        Assert.Equal(3, listed.Value.TotalPages);
    }

    [Theory]
    [InlineData("entityType")]
    [InlineData("entityId")]
    [InlineData("subjectType")]
    [InlineData("subjectId")]
    [InlineData("actorId")]
    [InlineData("operation")]
    [InlineData("correlationId")]
    [InlineData("occurredAt")]
    public async Task ListAsync_FilterOnAnAllowedField_ListsOnlyTheRecordsItMatches(string field)
    {
        var subject = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var entity = Guid.CreateVersion7();
        var created = Record(subject, Now.AddMinutes(-3), ChangeOperation.Created, PeopleKey, entity, actorId: Asha, correlationId: Guid.CreateVersion7().ToString("N"));
        var updated = Record(subject, Now.AddMinutes(-2), ChangeOperation.Updated, TeamsKey, actorId: Ravi, correlationId: Guid.CreateVersion7().ToString("N"));
        var deleted = Record(subject, Now.AddMinutes(-1), ChangeOperation.Deleted, PeopleKey, entity, TeamsKey, ActorIds.System);
        var elsewhere = Record(other, Now.AddMinutes(-1));
        await SaveAsync([created, updated, deleted, elsewhere]);

        var (filter, expected) = field switch
        {
            "entityType" => ($"entityType:eq:{TeamsKey}", new[] { updated }),
            "entityId" => ($"entityId:eq:{entity}", [created, deleted]),
            "subjectType" => ($"subjectType:in:{TeamsKey}", [deleted]),
            "subjectId" => ($"subjectId:ne:{subject}", [elsewhere]),
            "actorId" => ($"actorId:eq:{Ravi}", [updated]),
            "operation" => ("operation:eq:created", [created]),
            "correlationId" => ($"correlationId:eq:{updated.CorrelationId}", [updated]),
            _ => ($"occurredAt:gte:{Uri.EscapeDataString(Now.AddMinutes(-2).ToString("O", CultureInfo.InvariantCulture))}", [updated, deleted]),
        };
        var scope = field == "subjectId" ? $"subjectId:in:{subject}|{other}" : $"subjectId:eq:{subject}";

        var listed = await ListAsync(filter: $"{scope};{filter}");

        Assert.Equal(expected.Select(r => r.Id.Value).Order(), listed.Value.Items.Select(r => r.Id).Order());
    }

    [Theory]
    [InlineData("entityType", null)]
    [InlineData(null, "changes:eq:x")]
    [InlineData(null, "valueCount:eq:1")]
    [InlineData(null, "clientAddress:eq:198.51.100.7")]
    public async Task ListAsync_SortOrFilterOutsideTheAllowLists_IsRefusedAsAnInvalidField(string? sort, string? filter)
    {
        var listed = await ListAsync(sort: sort, filter: filter);

        Assert.Equal(QueryErrors.InvalidField, listed.Error?.Code);
    }

    [Theory]
    [InlineData("entityType:contains:test")]
    [InlineData("correlationId:contains:0192")]
    [InlineData("operation:gt:created")]
    public async Task ListAsync_OperatorAFieldDoesNotAllow_IsRefusedAsAnInvalidOperator(string filter)
    {
        var listed = await ListAsync(filter: filter);

        Assert.Equal(QueryErrors.InvalidOperator, listed.Error?.Code);
    }

    [Fact]
    public async Task ListAsync_NamedSubjectsReferencesAndPeople_ShowsTheirNamesAskingEachSourceOncePerPage()
    {
        var subject = Guid.CreateVersion7();
        var payroll = Guid.CreateVersion7();
        var audit = Guid.CreateVersion7();
        var stranger = Guid.CreateVersion7();
        var moved = Record(
            subject,
            Now.AddMinutes(-4),
            actorId: Asha,
            changes: Values(Reference("teamId", payroll, audit), """{"property":"displayName","kind":"text","before":"Asha","after":"Asha Rao"}"""),
            valueCount: 2);
        var bySystem = Record(subject, Now.AddMinutes(-3), actorId: ActorIds.System);
        var byNobody = Record(subject, Now.AddMinutes(-2), actorId: ActorIds.Anonymous);
        var byStranger = Record(subject, Now.AddMinutes(-1), actorId: stranger);
        await SaveAsync([moved, bySystem, byNobody, byStranger]);
        var people = new FakeRecordNames(PeopleKey, new Dictionary<Guid, string> { [subject] = "Asha Rao" });
        var teams = new FakeRecordNames(TeamsKey, new Dictionary<Guid, string> { [payroll] = "Payroll", [audit] = "Internal audit" });
        var personNames = new FakePersonNames(new Dictionary<Guid, string> { [Asha] = "Asha Rao (HR)" });

        var listed = await ListAsync(filter: $"subjectId:eq:{subject}", recordNames: [people, teams], personNames: [personNames]);

        var records = listed.Value.Items.ToDictionary(r => r.Id);
        Assert.All(records.Values, r => Assert.Equal("Asha Rao", r.SubjectName));
        var change = records[moved.Id.Value];
        Assert.Equal((ChangeActorKind.Person, "Asha Rao (HR)"), (change.ActorKind, change.ActorName));
        Assert.Equal(
            [
                new ChangedValueDetails("teamId", ChangeValueKind.Reference, payroll.ToString(), audit.ToString(), TeamsKey, "Payroll", "Internal audit"),
                new ChangedValueDetails("displayName", ChangeValueKind.Text, "Asha", "Asha Rao", null, null, null),
            ],
            change.Changes);
        Assert.Equal((ChangeActorKind.System, (string?)null), (records[bySystem.Id.Value].ActorKind, records[bySystem.Id.Value].ActorName));
        Assert.Equal((ChangeActorKind.Anonymous, (string?)null), (records[byNobody.Id.Value].ActorKind, records[byNobody.Id.Value].ActorName));
        Assert.Equal((ChangeActorKind.Person, (string?)null), (records[byStranger.Id.Value].ActorKind, records[byStranger.Id.Value].ActorName));
        Assert.Equal([subject], Assert.Single(people.Asked));
        Assert.Equal(new[] { payroll, audit }.Order(), Assert.Single(teams.Asked).Order());
        Assert.Equal(new[] { Asha, stranger }.Order(), Assert.Single(personNames.Asked).Order());
    }

    [Fact]
    public async Task ListAsync_NoSourceOfNames_ShowsTheIdsWithoutNames()
    {
        var subject = Guid.CreateVersion7();
        var team = Guid.CreateVersion7();
        var record = Record(subject, Now.AddMinutes(-1), actorId: Asha, changes: Values(Reference("teamId", null, team)), valueCount: 1);
        await SaveAsync([record]);

        var listed = await ListAsync(filter: $"subjectId:eq:{subject}");

        var details = Assert.Single(listed.Value.Items);
        Assert.Null(details.SubjectName);
        Assert.Null(details.ActorName);
        Assert.Equal(new ChangedValueDetails("teamId", ChangeValueKind.Reference, null, team.ToString(), TeamsKey, null, null), Assert.Single(details.Changes));
    }

    [Fact]
    public async Task Constructor_TwoSourcesNameOneChangeKey_Throws()
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();

        var failure = Assert.Throws<InvalidOperationException>(() => new ChangeHistory(
            context,
            [new FakeRecordNames(PeopleKey, new Dictionary<Guid, string>()), new FakeRecordNames(PeopleKey, new Dictionary<Guid, string>())],
            [],
            new FakeTimeProvider(Now)));

        Assert.Contains(PeopleKey, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_RecordsUpToItsStart_CountsTheirLinesAndReadsThemOldestFirst()
    {
        var subject = Guid.CreateVersion7();
        var withoutValues = Record(subject, Now.AddMinutes(-3), ChangeOperation.Deleted);
        var withTwo = Record(subject, Now.AddMinutes(-2), changes: Entries(2), valueCount: 2);
        var withThree = Record(subject, Now, ChangeOperation.Created, changes: Entries(3), valueCount: 3);
        var afterTheStart = Record(subject, Now.AddTicks(1), changes: Entries(1), valueCount: 1);
        await SaveAsync([withThree, afterTheStart, withoutValues, withTwo]);

        await using var scope = database.CreateScope();
        var export = await ExportAsync(scope, $"subjectId:eq:{subject}");

        Assert.Equal((3L, 6L, Now), (export.Value.RecordCount, export.Value.LineCount, export.Value.Until));
        var read = await ReadAllAsync(export.Value);
        Assert.Equal([withoutValues.Id.Value, withTwo.Id.Value, withThree.Id.Value], read.Select(r => r.Id));
        Assert.Equal([0, 2, 3], read.Select(r => r.Changes.Count));
    }

    [Fact]
    public async Task ExportAsync_ExactlyTheLineCap_IsAllowedAndOneLineMoreIsRefused()
    {
        var subject = Guid.CreateVersion7();
        var full = Enumerable.Range(1, 3).Select(minutes => Record(subject, Now.AddMinutes(-minutes), changes: Entries(short.MaxValue), valueCount: short.MaxValue));
        const int rest = ChangeExport.MaxLines - (3 * short.MaxValue);
        await SaveAsync([.. full, Record(subject, Now.AddMinutes(-4), changes: Entries(rest), valueCount: rest)]);

        await using var scope = database.CreateScope();
        var atTheCap = await ExportAsync(scope, $"subjectId:eq:{subject}");
        await SaveAsync([Record(subject, Now.AddMinutes(-5))]);
        var overTheCap = await ExportAsync(scope, $"subjectId:eq:{subject}");

        Assert.Equal((4L, (long)ChangeExport.MaxLines), (atTheCap.Value.RecordCount, atTheCap.Value.LineCount));
        Assert.Equal(ChangeHistoryErrors.ExportTooLarge, overTheCap.Error);
        Assert.Equal(("audit.export-too-large", ErrorKind.Failure), (overTheCap.Error!.Code, overTheCap.Error.Kind));
    }

    [Fact]
    public async Task ExportAsync_PageEndingInsideARunOfOneTime_ReadsEveryRecordOnceOldestFirst()
    {
        var subject = Guid.CreateVersion7();
        var start = Now.AddHours(-1);
        var before = Enumerable.Range(0, ChangeHistory.ExportPageSize - 2).Select(seconds => Record(subject, start.AddSeconds(seconds)));
        var runAt = start.AddSeconds(ChangeHistory.ExportPageSize);
        var run = Enumerable.Range(0, 5).Select(_ => Record(subject, runAt));
        var after = Enumerable.Range(1, 3).Select(seconds => Record(subject, runAt.AddSeconds(seconds)));
        ChangeRecord[] records = [.. before, .. run, .. after];
        await SaveAsync(records);

        await using var scope = database.CreateScope();
        var export = await ExportAsync(scope, $"subjectId:eq:{subject}");
        var read = await ReadAllAsync(export.Value);

        Assert.Equal(records.Length, export.Value.RecordCount);
        Assert.Equal(records.Select(r => r.Id.Value).Order(), read.Select(r => r.Id).Order());
        Assert.Equal(read.Select(r => r.OccurredAt).Order(), read.Select(r => r.OccurredAt));
    }

    [Fact]
    public async Task ExportAsync_NothingMatches_CountsNoLineAndReadsNothing()
    {
        await using var scope = database.CreateScope();
        var export = await ExportAsync(scope, $"subjectId:eq:{Guid.CreateVersion7()}");

        Assert.Equal((0L, 0L), (export.Value.RecordCount, export.Value.LineCount));
        Assert.Empty(await ReadAllAsync(export.Value));
    }

    [Fact]
    public async Task ExportAsync_FilterOutsideTheAllowList_IsRefusedAsAnInvalidField()
    {
        await using var scope = database.CreateScope();
        var export = await ExportAsync(scope, "changes:eq:x");

        Assert.Equal(QueryErrors.InvalidField, export.Error?.Code);
    }

    private static ChangeRecord Record(
        Guid subjectId,
        DateTimeOffset occurredAt,
        ChangeOperation operation = ChangeOperation.Updated,
        string entityType = PeopleKey,
        Guid? entityId = null,
        string subjectType = PeopleKey,
        Guid? actorId = null,
        string? correlationId = null,
        string? changes = null,
        short valueCount = 0) =>
        ChangeRecord.Record(
            new ChangeStamp(occurredAt, actorId ?? Asha, "198.51.100.7", correlationId),
            operation,
            entityType,
            entityId ?? Guid.CreateVersion7(),
            subjectType,
            subjectId,
            changes,
            valueCount);

    private static string Values(params string[] entries) => $"[{string.Join(',', entries)}]";

    private static string Reference(string property, Guid? before, Guid after) =>
        $$"""{"property":"{{property}}","kind":"reference","before":{{(before is { } id ? $"\"{id}\"" : "null")}},"after":"{{after}}","references":"{{TeamsKey}}"}""";

    private static string Entries(int count) => Values([.. Enumerable.Repeat("""{"property":"note","kind":"text","after":"x"}""", count)]);

    private static async Task<List<ChangeRecordDetails>> ReadAllAsync(ChangeExport export)
    {
        var read = new List<ChangeRecordDetails>();
        await foreach (var details in export.Read(TestContext.Current.CancellationToken))
        {
            read.Add(details);
        }

        return read;
    }

    private async Task SaveAsync(IReadOnlyCollection<ChangeRecord> records)
    {
        await using var scope = database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditingDbContext>();
        context.ChangeRecords.AddRange(records);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Result<PagedResult<ChangeRecordDetails>>> ListAsync(
        int? page = null,
        int? pageSize = null,
        string? sort = null,
        string? filter = null,
        IRecordNames[]? recordNames = null,
        IPersonNames[]? personNames = null)
    {
        await using var scope = database.CreateScope();
        var history = new ChangeHistory(scope.ServiceProvider.GetRequiredService<AuditingDbContext>(), recordNames ?? [], personNames ?? [], new FakeTimeProvider(Now));
        var request = ListRequest.Parse(page, pageSize, sort, filter);

        return request.IsFailure ? request.Error! : await history.ListAsync(request.Value, TestContext.Current.CancellationToken);
    }

    private static Task<Result<ChangeExport>> ExportAsync(AsyncServiceScope scope, string filter) =>
        new ChangeHistory(scope.ServiceProvider.GetRequiredService<AuditingDbContext>(), [], [], new FakeTimeProvider(Now))
            .ExportAsync(FilterRequest.Parse(filter).Value, TestContext.Current.CancellationToken);

    private sealed class FakeRecordNames(string key, IReadOnlyDictionary<Guid, string> names) : IRecordNames
    {
        public List<IReadOnlyCollection<Guid>> Asked { get; } = [];

        public IReadOnlySet<string> EntityTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { key };

        public Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        {
            Asked.Add([.. ids]);

            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(names.Where(pair => ids.Contains(pair.Key)).ToDictionary());
        }
    }

    private sealed class FakePersonNames(IReadOnlyDictionary<Guid, string> names) : IPersonNames
    {
        public List<IReadOnlyCollection<Guid>> Asked { get; } = [];

        public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken)
        {
            Asked.Add([.. objectIds]);

            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(names.Where(pair => objectIds.Contains(pair.Key)).ToDictionary());
        }
    }
}
