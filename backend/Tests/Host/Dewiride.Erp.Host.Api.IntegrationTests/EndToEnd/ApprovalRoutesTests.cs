using System.Globalization;
using System.Net;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Queries.Paging;
using Dewiride.Erp.BuildingBlocks.Approvals.Administration;
using Dewiride.Erp.BuildingBlocks.Approvals.Deciding;
using Dewiride.Erp.BuildingBlocks.Approvals.Domain;
using Dewiride.Erp.BuildingBlocks.Approvals.Inbox;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Persistence.Versioning;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Approvals;
using Dewiride.Erp.Testing.Authentication;
using Dewiride.Erp.Testing.EndToEnd;
using Dewiride.Erp.Testing.EndToEnd.Approvals;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace Dewiride.Erp.Host.Api.IntegrationTests.EndToEnd;

// Each test that calls the route as somebody signs a new person of the accountant persona in, as a spec does, and reads the requests
// it submitted through the inbox the Approvals page reads, as that person. The requests wait for Finance, a step routed without the
// people's reporting lines, so the tests depend on the route alone, each on a record of its own; the one test that switches a kind
// off takes an API kind no other test takes.
public sealed class ApprovalRoutesTests(ApprovalRoutesTests.Fixture fixture) : IClassFixture<ApprovalRoutesTests.Fixture>
{
    private const string AlreadySubmittedCode = "approval.already-submitted";

    public static TheoryData<Guid> NotAPerson => [ActorIds.System, ActorIds.Anonymous];

    [Fact]
    public async Task Post_ATestKindFromASession_SubmitsTheRequestAsThePersonSignedInForThemselves()
    {
        var kind = TestApprovalKinds.Finance(1);
        using var client = TestSignIn.CreateClient(fixture.Factory);
        var person = await SignInAsync(client);

        var id = await SubmitAsync(client, new { kind = kind.Key });

        var details = await RequestAsync(id, person.Id);
        var request = details.Request;
        Assert.Equal((kind.Key, kind.Title, TestApprovalKinds.SubjectType, ApprovalState.Submitted), (request.Kind, request.KindTitle, request.SubjectType, request.State));
        Assert.Equal((person.Id, person.Id), (request.RequestedForId, request.SubmittedById));
        Assert.Equal(person.Name, request.RequestedForName);
        Assert.Equal(TestApprovalKinds.FinanceStepTitle, request.WaitingStepTitle);
        Assert.Equal(ApproverKind.Role, request.WaitingForKind);
        Assert.Equal(ErpRole.Finance, request.WaitingForRole);
        Assert.False(request.Late);
        Assert.Equal((false, true), (details.CanDecide, details.CanWithdraw));
    }

    [Fact]
    public async Task Post_ForAnotherPerson_SubmitsTheRequestForThatPersonAsThePersonSignedIn()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        var person = await SignInAsync(client);
        var asker = Guid.CreateVersion7();

        var id = await SubmitAsync(client, new { kind = TestApprovalKinds.Finance(1).Key, requestedFor = asker });

        var request = (await RequestAsync(id, person.Id)).Request;
        Assert.Equal((asker, person.Id), (request.RequestedForId, request.SubmittedById));
    }

    [Fact]
    public async Task Post_SubmittedWorkingDaysAgo_SubmitsTheRequestThatManyWorkingDaysBeforeTheCallSoItsStepIsLate()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        var person = await SignInAsync(client);
        var before = TimeProvider.System.GetUtcNow();

        var id = await SubmitAsync(client, new { kind = TestApprovalKinds.Finance(1).Key, submittedWorkingDaysAgo = 5 });

        var after = TimeProvider.System.GetUtcNow();
        var details = await RequestAsync(id, person.Id);
        var submittedAt = details.Request.SubmittedAt;
        var step = Assert.Single(details.Steps);
        Assert.InRange(submittedAt, before.AddDays(-7), after.AddDays(-5));
        Assert.False(IndianStandardTime.IstDate(submittedAt).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Equal(submittedAt, step.StartedAt);
        Assert.Equal(TestApprovalKinds.WorkingDaysToDecide, step.WorkingDays);
        Assert.True(step.DueAt > step.StartedAt);
        Assert.True(step.Late);
        Assert.True(details.Request.Late);
    }

    [Theory]
    [InlineData("2026-11-25T10:00:00+05:30", 0, "2026-11-25T10:00:00+05:30")]
    [InlineData("2026-11-29T00:00:00+05:30", 0, "2026-11-29T00:00:00+05:30")]
    [InlineData("2026-11-25T10:00:00+05:30", 3, "2026-11-20T10:00:00+05:30")]
    [InlineData("2026-11-23T09:15:00+05:30", 1, "2026-11-20T09:15:00+05:30")]
    [InlineData("2026-11-28T23:59:59+05:30", 1, "2026-11-27T23:59:59+05:30")]
    [InlineData("2026-11-29T00:00:00+05:30", 5, "2026-11-23T00:00:00+05:30")]
    [InlineData("2026-11-27T20:30:00+00:00", 1, "2026-11-27T02:00:00+05:30")]
    [InlineData("2027-01-01T10:00:00+05:30", 30, "2026-11-20T10:00:00+05:30")]
    public void SubmittedAt_WorkingDaysBeforeAnInstant_CountsMondayToFridayInIndiaAndKeepsTheTimeOfDay(string now, int workingDaysAgo, string expected)
    {
        var submittedAt = ApprovalRequestSeeder.SubmittedAt(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture), workingDaysAgo);

        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), submittedAt);
        Assert.Equal(TimeSpan.Zero, submittedAt.Offset);
    }

    [Fact]
    public async Task Post_AKindTheAdministratorsSwitchedOff_AnswersNoRequestAndSubmitsNone()
    {
        var kind = TestApprovalKinds.Api(12);
        await SwitchOffAsync(kind.Key);
        using var client = TestSignIn.CreateClient(fixture.Factory);
        var person = await SignInAsync(client);
        var subjectId = Guid.CreateVersion7();

        using var response = await PostAsync(client, new { kind = kind.Key, subjectId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadFromJsonAsync<ApprovalRequestSubmissionResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(answer);
        Assert.Null(answer.RequestId);
        Assert.Empty(await RequestsOfAsync(person.Id, subjectId));
    }

    [Fact]
    public async Task Post_TheRecordOfARequestStillWaiting_Answers409()
    {
        var kind = TestApprovalKinds.Finance(2);
        using var client = TestSignIn.CreateClient(fixture.Factory);
        await SignInAsync(client);
        var subjectId = Guid.CreateVersion7();
        await SubmitAsync(client, new { kind = kind.Key, subjectId });

        using var again = await PostAsync(client, new { kind = kind.Key, subjectId });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var problem = await again.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(AlreadySubmittedCode, problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Post_TheRecordOfAWithdrawnRequest_SubmitsANewRequestThatListsTheWithdrawnOneBeforeIt()
    {
        var kind = TestApprovalKinds.Finance(3);
        using var client = TestSignIn.CreateClient(fixture.Factory);
        var person = await SignInAsync(client);
        var subjectId = Guid.CreateVersion7();
        var first = await SubmitAsync(client, new { kind = kind.Key, subjectId });
        await WithdrawAsync(first, person.Id);

        var second = await SubmitAsync(client, new { kind = kind.Key, subjectId });

        var details = await RequestAsync(second, person.Id);
        Assert.Equal((subjectId, ApprovalState.Submitted), (details.Request.SubjectId, details.Request.State));
        var earlier = Assert.Single(details.EarlierRequests);
        Assert.Equal((first, ApprovalState.Withdrawn), (earlier.Id.Value, earlier.State));
    }

    [Fact]
    public async Task Post_ATestKind_AnswersTheRequestIdInThisShape()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        await SignInAsync(client);

        using var response = await PostAsync(client, new { kind = TestApprovalKinds.Finance(4).Key });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["requestId"], body.RootElement.EnumerateObject().Select(member => member.Name));
        Assert.True(body.RootElement.GetProperty("requestId").TryGetGuid(out _));
    }

    [Fact]
    public async Task Post_FromASessionThatSendsNoAntiforgeryToken_SubmitsTheRequest()
    {
        using var client = fixture.Factory.CreateDefaultClient(TestSignIn.BaseAddress, new CookieContainerHandler(new CookieContainer()));
        var person = await SignInAsync(client);

        var id = await SubmitAsync(client, new { kind = TestApprovalKinds.Finance(4).Key });

        Assert.Equal(person.Id, (await RequestAsync(id, person.Id)).Request.SubmittedById);
    }

    [Fact]
    public async Task Post_WithoutASession_Answers401()
    {
        using var anonymous = fixture.Factory.CreateClient();

        using var response = await PostAsync(anonymous, new { kind = TestApprovalKinds.Finance(5).Key });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(ProblemTypes.RequestUnauthenticated, problem!.Extensions["code"]?.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("finance.expenses.bill-approval")]
    [InlineData("test.approvals.manager-13")]
    public async Task Post_AKindOtherThanTheTestKinds_Answers400(string? kind)
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        await SignInAsync(client);

        using var response = await PostAsync(client, new { kind });

        Assert.Equal(["kind"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Post_FieldsNoRequestCouldHave_Answers400ForEach()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        await SignInAsync(client);

        using var response = await PostAsync(
            client,
            new { kind = TestApprovalKinds.Finance(6).Key, requestedFor = Guid.Empty, submittedWorkingDaysAgo = ApprovalRequestSeeder.MaxWorkingDaysAgo + 1, subjectId = Guid.Empty });

        Assert.Equal(["requestedFor", "subjectId", "submittedWorkingDaysAgo"], await ErrorKeysAsync(response));
    }

    [Theory]
    [MemberData(nameof(NotAPerson))]
    public async Task Post_ForTheSystemOrAnAnonymousCaller_Answers400(Guid requestedFor)
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        await SignInAsync(client);

        using var response = await PostAsync(client, new { kind = TestApprovalKinds.Finance(6).Key, requestedFor });

        Assert.Equal(["requestedFor"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Post_SubmittedFewerThanNoWorkingDaysAgo_Answers400()
    {
        using var client = TestSignIn.CreateClient(fixture.Factory);
        await SignInAsync(client);

        using var response = await PostAsync(client, new { kind = TestApprovalKinds.Finance(6).Key, submittedWorkingDaysAgo = -1 });

        Assert.Equal(["submittedWorkingDaysAgo"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Map_InTheDevelopmentEnvironment_StopsTheHostFromStarting()
    {
        await using var factory = new ErpApiFactory().WithTestEndpoints(ApprovalRoutes.Map);

        var exception = Assert.IsType<InvalidOperationException>(factory.StartFailure());

        Assert.Contains(EndToEndHost.EnvironmentName, exception.Message, StringComparison.Ordinal);
    }

    private static ApprovalCaller CallerOf(Guid person) => new(person, new HashSet<ErpRole> { ErpRole.Employee }, SeesAll: false, DecidesAny: false);

    private static async Task<PersonaSignInResponse> SignInAsync(HttpClient client)
    {
        using var response = await client.PostAsync(new Uri($"{PersonaSignIn.PathPrefix}/accountant", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await response.Content.ReadFromJsonAsync<PersonaSignInResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(person);

        return person;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, object submission) =>
        client.PostAsJsonAsync(new Uri(ApprovalRoutes.RequestsPath, UriKind.Relative), submission, TestContext.Current.CancellationToken);

    private static async Task<Guid> SubmitAsync(HttpClient client, object submission)
    {
        using var response = await PostAsync(client, submission);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadFromJsonAsync<ApprovalRequestSubmissionResponse>(TestContext.Current.CancellationToken);

        return Assert.NotNull(answer?.RequestId);
    }

    private static async Task<IReadOnlyList<string>> ErrorKeysAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);

        return [.. problem!.Errors.Keys.Order(StringComparer.Ordinal)];
    }

    private async Task<ApprovalRequestDetails> RequestAsync(Guid id, Guid person)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var details = await scope.ServiceProvider.GetRequiredService<IApprovalInbox>().GetAsync(ApprovalRequestId.From(id), CallerOf(person), TestContext.Current.CancellationToken);
        Assert.True(details.IsSuccess, details.Error?.Code);

        return details.Value;
    }

    private async Task<IReadOnlyList<ApprovalRequestSummary>> RequestsOfAsync(Guid person, Guid subjectId)
    {
        var list = ListRequest.Parse(page: null, pageSize: null, sort: null, filter: $"subjectId:eq:{subjectId:D}");
        Assert.True(list.IsSuccess, list.Error?.Code);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var page = await scope.ServiceProvider.GetRequiredService<IApprovalInbox>().ListAsync(ApprovalBox.Mine, list.Value, CallerOf(person), TestContext.Current.CancellationToken);
        Assert.True(page.IsSuccess, page.Error?.Code);

        return page.Value.Items;
    }

    private async Task SwitchOffAsync(string kind)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var set = await scope.ServiceProvider.GetRequiredService<IApprovalAdministration>()
            .SetUpAsync(kind, required: false, TestApprovalKinds.WorkingDaysToDecide, TestContext.Current.CancellationToken);
        Assert.True(set.IsSuccess, set.Error?.Code);
    }

    private async Task WithdrawAsync(Guid id, Guid person)
    {
        var details = await RequestAsync(id, person);
        Assert.True(RecordVersion.TryDecode(details.Version, out var version));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var withdrawn = await scope.ServiceProvider.GetRequiredService<IApprovalDecisions>()
            .WithdrawAsync(ApprovalRequestId.From(id), comment: null, version, CallerOf(person), TestContext.Current.CancellationToken);
        Assert.True(withdrawn.IsSuccess, withdrawn.Error?.Code);
    }

    public sealed class Fixture : IAsyncDisposable
    {
        public ErpApiFactory Factory { get; } = ErpApiFactory.ForEnvironment(EndToEndHost.EnvironmentName)
            .WithTestServices(TestApprovalKinds.Register)
            .WithTestEndpoints(PersonaSignIn.Map)
            .WithTestEndpoints(ApprovalRoutes.Map);

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }
}
