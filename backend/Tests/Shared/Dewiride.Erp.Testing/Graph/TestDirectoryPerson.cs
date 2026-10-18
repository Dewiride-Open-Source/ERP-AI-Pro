using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Testing.Graph;

public sealed record TestDirectoryPerson(
    Guid ObjectId,
    string DisplayName,
    string UserPrincipalName,
    string? Mail,
    TestDirectoryPhoto? Photo = null,
    DirectoryAccountState AccountState = DirectoryAccountState.Enabled)
{
    public static TestDirectoryPerson Of(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new TestDirectoryPerson(user.ObjectId, user.Name, user.UserName, user.UserName);
    }
}
