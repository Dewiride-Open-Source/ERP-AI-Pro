using Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Authentication.Sessions;

public sealed class FrontChannelSignOutTests
{
    private const string TenantId = "5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d";

    private static readonly Guid Member = Guid.Parse("6f1e2d3c-4b5a-4968-8776-a5b4c3d2e1f0");

    [Theory]
    [InlineData("6f1e2d3c-4b5a-4968-8776-a5b4c3d2e1f0.5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d")]
    [InlineData("6F1E2D3C-4B5A-4968-8776-A5B4C3D2E1F0.5D7C3B9A-1E2F-4A6B-8C0D-9E8F7A6B5C4D")]
    public void ObjectIdOf_AccountWhoseHomeIsThisTenant_IsItsObjectId(string accountId)
    {
        Assert.Equal(Member, FrontChannelSignOut.ObjectIdOf(accountId, TenantId));
    }

    [Theory]
    [InlineData("6f1e2d3c-4b5a-4968-8776-a5b4c3d2e1f0.9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d")]
    [InlineData("00000000-0000-0000-0040-34a7f8d6c2e1.9188040d-6c67-4c5b-b112-36a304b66dad")]
    public void ObjectIdOf_GuestWhoseHomeIsAnotherTenant_NamesNoAccount(string accountId)
    {
        Assert.Null(FrontChannelSignOut.ObjectIdOf(accountId, TenantId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("6f1e2d3c-4b5a-4968-8776-a5b4c3d2e1f0")]
    [InlineData(".5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d")]
    [InlineData("not-an-object-id.5d7c3b9a-1e2f-4a6b-8c0d-9e8f7a6b5c4d")]
    [InlineData("6f1e2d3c-4b5a-4968-8776-a5b4c3d2e1f0.")]
    public void ObjectIdOf_AccountIdThatIsMalformed_NamesNoAccount(string accountId)
    {
        Assert.Null(FrontChannelSignOut.ObjectIdOf(accountId, TenantId));
    }
}
