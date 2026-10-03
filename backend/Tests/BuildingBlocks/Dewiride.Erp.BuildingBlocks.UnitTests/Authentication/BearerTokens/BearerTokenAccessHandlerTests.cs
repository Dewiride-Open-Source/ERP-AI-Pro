using System.Security.Claims;
using Dewiride.Erp.BuildingBlocks.Authentication.BearerTokens;
using Microsoft.AspNetCore.Authorization;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.BearerTokens;

public sealed class BearerTokenAccessHandlerTests
{
    private const string ObjectId = "b47e2c91-5d3a-4e8f-9c6b-2a1f0e9d8c73";

    private static readonly BearerTokenAccess Access = new(["Erp.Read"], ["Erp.Integration"]);

    [Fact]
    public void IsGranted_PersonWithAGrantedScope_IsTrue()
    {
        Assert.True(BearerTokenAccessHandler.IsGranted(Token(("scp", "Erp.Write Erp.Read")), Access));
    }

    [Fact]
    public void IsGranted_PersonWithoutAGrantedScope_IsFalse()
    {
        Assert.False(BearerTokenAccessHandler.IsGranted(Token(("scp", "Erp.Write erp.read")), Access));
    }

    [Fact]
    public void IsGranted_PersonHoldingTheApplicationRoleAsTheirOwnRole_IsFalse()
    {
        Assert.False(BearerTokenAccessHandler.IsGranted(Token(("scp", "Erp.Write"), ("roles", "Erp.Integration")), Access));
    }

    [Fact]
    public void IsGranted_ApplicationWithAGrantedRole_IsTrue()
    {
        Assert.True(BearerTokenAccessHandler.IsGranted(Token(("idtyp", "app"), ("roles", "Erp.Reports"), ("roles", "Erp.Integration")), Access));
    }

    [Fact]
    public void IsGranted_ApplicationWithoutAGrantedRole_IsFalse()
    {
        Assert.False(BearerTokenAccessHandler.IsGranted(Token(("oid", ObjectId), ("sub", ObjectId), ("roles", "Erp.Read")), Access));
    }

    [Fact]
    public void IsGranted_TokenOfNoHolder_IsFalse()
    {
        Assert.False(BearerTokenAccessHandler.IsGranted(Token(("oid", ObjectId), ("sub", "pairwise"), ("roles", "Erp.Integration")), Access));
    }

    [Fact]
    public async Task HandleAsync_GrantedAndRefusedTokens_SucceedOnlyTheGrantedRequirement()
    {
        var handler = new BearerTokenAccessHandler();
        var requirement = new BearerTokenAccessRequirement(Access);
        var granted = new AuthorizationHandlerContext([requirement], Token(("scp", "Erp.Read")), resource: null);
        var refused = new AuthorizationHandlerContext([requirement], Token(("scp", "Erp.Write")), resource: null);

        await handler.HandleAsync(granted);
        await handler.HandleAsync(refused);

        Assert.True(granted.HasSucceeded);
        Assert.False(refused.HasSucceeded);
    }

    private static ClaimsPrincipal Token(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)), "Bearer"));
}
