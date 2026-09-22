using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Actors;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Endpoints.Actors;

public sealed class HttpActorContextTests
{
    private const string AuthenticationType = "Test";

    private static readonly Guid ObjectId = new("0199a1b2-0000-7000-8000-00000000abcd");

    private static readonly Guid OtherObjectId = new("0199a1b2-0000-7000-8000-00000000ef01");

    [Fact]
    public void ActorId_NoHttpContext_IsTheSystemActorUnauthenticated()
    {
        var context = new HttpActorContext(new HttpContextAccessor { HttpContext = null });

        Assert.Equal(ActorIds.System, context.ActorId);
        Assert.False(context.IsAuthenticated);
    }

    [Fact]
    public void ActorId_UnauthenticatedPrincipal_IsTheAnonymousActorUnauthenticated()
    {
        var context = new HttpActorContext(Accessor(new ClaimsPrincipal(new ClaimsIdentity())));

        Assert.Equal(ActorIds.Anonymous, context.ActorId);
        Assert.False(context.IsAuthenticated);
    }

    [Fact]
    public void ActorId_UnauthenticatedPrincipalCarryingAnOid_IsStillTheAnonymousActor()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(HttpActorContext.ObjectIdentifierClaim, ObjectId.ToString())]));

        var context = new HttpActorContext(Accessor(principal));

        Assert.Equal(ActorIds.Anonymous, context.ActorId);
        Assert.False(context.IsAuthenticated);
    }

    [Fact]
    public void ActorId_AuthenticatedWithTheObjectIdentifierClaim_IsThatGuidAuthenticated()
    {
        var context = new HttpActorContext(Accessor(Authenticated(new Claim(HttpActorContext.ObjectIdentifierClaim, ObjectId.ToString()))));

        Assert.Equal(ObjectId, context.ActorId);
        Assert.True(context.IsAuthenticated);
    }

    [Fact]
    public void ActorId_AuthenticatedWithOnlyTheShortOidClaim_IsThatGuidAuthenticated()
    {
        var context = new HttpActorContext(Accessor(Authenticated(new Claim(HttpActorContext.ShortObjectIdentifierClaim, ObjectId.ToString()))));

        Assert.Equal(ObjectId, context.ActorId);
        Assert.True(context.IsAuthenticated);
    }

    [Fact]
    public void ActorId_AuthenticatedWithBothClaims_PrefersTheObjectIdentifierClaim()
    {
        var principal = Authenticated(
            new Claim(HttpActorContext.ShortObjectIdentifierClaim, OtherObjectId.ToString()),
            new Claim(HttpActorContext.ObjectIdentifierClaim, ObjectId.ToString()));

        var context = new HttpActorContext(Accessor(principal));

        Assert.Equal(ObjectId, context.ActorId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void ActorId_AuthenticatedWithoutAParseableOid_ThrowsInvalidOperationException(string? oid)
    {
        Claim[] claims = oid is null ? [] : [new Claim(HttpActorContext.ObjectIdentifierClaim, oid)];
        var context = new HttpActorContext(Accessor(Authenticated(claims)));

        var exception = Assert.Throws<InvalidOperationException>(() => context.ActorId);

        Assert.Contains("oid", exception.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => context.IsAuthenticated);
    }

    [Fact]
    public void ActorId_ReadAfterTheHttpContextChanges_IsResolvedOnce()
    {
        var accessor = Accessor(Authenticated(new Claim(HttpActorContext.ObjectIdentifierClaim, ObjectId.ToString())));
        var context = new HttpActorContext(accessor);
        var first = context.ActorId;

        accessor.HttpContext = new DefaultHttpContext { User = Authenticated(new Claim(HttpActorContext.ObjectIdentifierClaim, OtherObjectId.ToString())) };

        Assert.Equal(ObjectId, first);
        Assert.Equal(ObjectId, context.ActorId);
        Assert.True(context.IsAuthenticated);
    }

    [Fact]
    public void ActorId_HttpContextAssignedAfterConstruction_IsReadAtFirstUse()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var context = new HttpActorContext(accessor);

        accessor.HttpContext = new DefaultHttpContext { User = Authenticated(new Claim(HttpActorContext.ObjectIdentifierClaim, ObjectId.ToString())) };

        Assert.Equal(ObjectId, context.ActorId);
        Assert.True(context.IsAuthenticated);
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpActorContext(null!));
    }

    private static HttpContextAccessor Accessor(ClaimsPrincipal user) =>
        new() { HttpContext = new DefaultHttpContext { User = user } };

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, AuthenticationType));
}
