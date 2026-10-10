using System.Net;
using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.SecurityEvents;

public sealed class SignInAuditTests
{
    private const string Correlation = "4bf92f3577b34da6a3ce929d0e0e4736";

    private const string Detail = "identity-provider:access_denied";

    private static readonly Guid Person = Guid.Parse("6f1e2d3c-4b5a-4968-8776-a5b4c3d2e1f0");

    private static readonly Guid Application = Guid.Parse("3f2c1b0a-9e8d-4c7b-a6f5-e4d3c2b1a090");

    [Fact]
    public async Task RecordAsync_Event_RecordsItWithTheClientAddressAndTheCorrelationIdOfTheRequest()
    {
        var recorder = new RecordingSecurityEventRecorder();
        await using var services = Services(recorder);

        await SignInAudit.RecordAsync(Request(services, "203.0.113.7"), SecurityEventKind.SignInFailed, Detail, Person, Application);

        Assert.Equal(new SecurityEventEntry(SecurityEventKind.SignInFailed, Detail, Person, Application, "203.0.113.7", Correlation), Assert.Single(recorder.Entries));
    }

    [Theory]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::7", "2001:db8::7")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    public async Task RecordAsync_ClientAddress_IsRecordedInTheFamilyItWasSentIn(string address, string recorded)
    {
        var recorder = new RecordingSecurityEventRecorder();
        await using var services = Services(recorder);

        await SignInAudit.RecordAsync(Request(services, address), SecurityEventKind.SignedOut, actor: Person);

        Assert.Equal(recorded, Assert.Single(recorder.Entries).ClientAddress);
    }

    [Fact]
    public async Task RecordAsync_RequestWithoutAClientAddress_RecordsNoAddress()
    {
        var recorder = new RecordingSecurityEventRecorder();
        await using var services = Services(recorder);

        await SignInAudit.RecordAsync(Request(services, address: null), SecurityEventKind.SignedOut, actor: Person);

        Assert.Null(Assert.Single(recorder.Entries).ClientAddress);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public async Task RecordAsync_RequestWithoutACorrelationId_RecordsItsTraceIdentifierWhenItFits(int length, bool recorded)
    {
        var recorder = new RecordingSecurityEventRecorder();
        await using var services = Services(recorder);
        var context = Request(services, "203.0.113.7", correlationId: null);
        context.TraceIdentifier = new string('a', length);

        await SignInAudit.RecordAsync(context, SecurityEventKind.SignedOut, actor: Person);

        Assert.Equal(recorded ? context.TraceIdentifier : null, Assert.Single(recorder.Entries).CorrelationId);
    }

    [Fact]
    public async Task RecordAsync_RecordThatCannotBeWritten_FailsWithTheRecordersFailure()
    {
        var recorder = new RecordingSecurityEventRecorder { Refused = new HashSet<SecurityEventKind> { SecurityEventKind.SignedIn } };
        await using var services = Services(recorder);

        var failure = await Assert.ThrowsAsync<TimeoutException>(() => SignInAudit.RecordAsync(Request(services, "203.0.113.7"), SecurityEventKind.SignedIn, actor: Person));

        Assert.Same(recorder.Failure, failure);
    }

    [Fact]
    public async Task TryRecordAsync_RecordThatCannotBeWritten_LogsTheFailureByKindAndReturns()
    {
        var recorder = new RecordingSecurityEventRecorder { Refused = new HashSet<SecurityEventKind> { SecurityEventKind.SignInFailed } };
        await using var services = Services(recorder);

        await SignInAudit.TryRecordAsync(Request(services, "203.0.113.7"), SecurityEventKind.SignInFailed, Detail);

        var record = Assert.Single(services.GetRequiredService<FakeLogCollector>().GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(typeof(SignInAudit).FullName, record.Category);
        Assert.Equal(nameof(SecurityEventKind.SignInFailed), record.GetStructuredStateValue("Kind"));
        Assert.Same(recorder.Failure, record.Exception);
        Assert.Empty(recorder.Entries);
    }

    [Fact]
    public async Task TryRecordAsync_RecordThatWasCancelled_LetsTheCancellationThrough()
    {
        var recorder = new RecordingSecurityEventRecorder { Refused = new HashSet<SecurityEventKind> { SecurityEventKind.SignedOut }, Failure = new OperationCanceledException() };
        await using var services = Services(recorder);

        await Assert.ThrowsAsync<OperationCanceledException>(() => SignInAudit.TryRecordAsync(Request(services, "203.0.113.7"), SecurityEventKind.SignedOut, actor: Person));

        Assert.Empty(services.GetRequiredService<FakeLogCollector>().GetSnapshot());
    }

    [Fact]
    public async Task TryRecordAsync_EventsWithoutAnActorOverTheBudget_AreCountedNotRecorded()
    {
        var recorder = new RecordingSecurityEventRecorder();
        await using var services = Services(recorder);

        for (var i = 0; i <= AnonymousSecurityEventBudget.EventsPerWindow; i++)
        {
            await SignInAudit.TryRecordAsync(Request(services, "203.0.113.7"), SecurityEventKind.SignInFailed, "callback:none");
        }

        Assert.Equal(AnonymousSecurityEventBudget.EventsPerWindow, recorder.Entries.Count);
        Assert.Contains(services.GetRequiredService<FakeLogCollector>().GetSnapshot(), record => record.Category == typeof(AnonymousSecurityEventBudget).FullName && record.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task TryRecordAsync_EventWithAnActorOnceTheBudgetIsSpent_IsStillRecorded()
    {
        var recorder = new RecordingSecurityEventRecorder();
        await using var services = Services(recorder);
        for (var i = 0; i <= AnonymousSecurityEventBudget.EventsPerWindow; i++)
        {
            await SignInAudit.TryRecordAsync(Request(services, "203.0.113.7"), SecurityEventKind.FrontChannelSignOutRefused, "other-issuer");
        }

        await SignInAudit.TryRecordAsync(Request(services, "203.0.113.7"), SecurityEventKind.SignedOut, actor: Person);

        Assert.Equal(new SecurityEventEntry(SecurityEventKind.SignedOut, ActorObjectId: Person, ClientAddress: "203.0.113.7", CorrelationId: Correlation), recorder.Entries[^1]);
    }

    [Fact]
    public void ObjectIdOf_PrincipalWithAnObjectId_IsThatObjectId()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", Person.ToString())], "Cookies"));

        Assert.Equal(Person, SignInAudit.ObjectIdOf(principal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-object-id")]
    public void ObjectIdOf_PrincipalWithoutAnObjectId_IsNone(string? objectId)
    {
        var claims = objectId is null ? [] : new[] { new Claim("oid", objectId) };

        Assert.Null(SignInAudit.ObjectIdOf(new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"))));
        Assert.Null(SignInAudit.ObjectIdOf(null));
    }

    private static ServiceProvider Services(RecordingSecurityEventRecorder recorder)
    {
        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddSingleton<ISecurityEventRecorder>(recorder);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<AnonymousSecurityEventBudget>();

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext Request(IServiceProvider services, string? address, string? correlationId = Correlation)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);
        if (correlationId is not null)
        {
            context.Features.Set<ICorrelationIdFeature>(new CorrelationIdFeature(correlationId));
        }

        return context;
    }
}
