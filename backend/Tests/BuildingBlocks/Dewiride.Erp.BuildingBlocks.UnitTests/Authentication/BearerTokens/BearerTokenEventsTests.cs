using Dewiride.Erp.BuildingBlocks.Auditing.Security;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Dewiride.Erp.BuildingBlocks.Authentication.Options;
using Dewiride.Erp.BuildingBlocks.Authentication.SecurityEvents;
using Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.SecurityEvents;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.BearerTokens;

public sealed class BearerTokenEventsTests
{
    private static readonly Guid Application = Guid.Parse("3f2c1b0a-9e8d-4c7b-a6f5-e4d3c2b1a090");

    [Fact]
    public async Task AuthenticationFailed_RefusedToken_IsRecordedWithItsReasonAndTheClientApplicationItClaims()
    {
        var recorder = new RecordingSecurityEventRecorder();
        await using var services = Services(recorder);
        var context = FailedContext(services, new SecurityTokenExpiredException("The token is expired."));

        await Events().AuthenticationFailed(context);

        var entry = Assert.Single(recorder.Entries);
        Assert.Equal((SecurityEventKind.BearerTokenRefused, "expired", Application, null), (entry.Kind, entry.Detail, entry.ClientApplicationId, entry.ActorObjectId));
    }

    [Fact]
    public async Task AuthenticationFailed_RequestAbandonedWhileItsTokenWasRead_IsNoRefusal()
    {
        var recorder = new RecordingSecurityEventRecorder();
        await using var services = Services(recorder);
        var context = FailedContext(services, new OperationCanceledException());

        await Events().AuthenticationFailed(context);

        Assert.Empty(recorder.Entries);
    }

    private static BearerTokenEvents Events() => new(Microsoft.Extensions.Options.Options.Create(new EntraSignInOptions()), new UnusedProblemDetails());

    private static ServiceProvider Services(RecordingSecurityEventRecorder recorder)
    {
        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddSingleton<ISecurityEventRecorder>(recorder);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<AnonymousSecurityEventBudget>();

        return services.BuildServiceProvider();
    }

    private static AuthenticationFailedContext FailedContext(IServiceProvider services, Exception failure)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Headers.Authorization = $"Bearer {new JsonWebTokenHandler().CreateToken($$"""{"azp":"{{Application}}"}""")}";

        return new AuthenticationFailedContext(httpContext, new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, displayName: null, typeof(JwtBearerHandler)), new JwtBearerOptions())
        {
            Exception = failure,
        };
    }

    private sealed class UnusedProblemDetails : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context) => throw new NotSupportedException("A failed authentication writes no problem.");
    }
}
