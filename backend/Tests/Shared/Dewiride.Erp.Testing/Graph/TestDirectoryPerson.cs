using Dewiride.Erp.Testing.Authentication;

namespace Dewiride.Erp.Testing.Graph;

public sealed record TestDirectoryPerson(Guid ObjectId, string DisplayName, string UserPrincipalName, string? Mail)
{
    public static TestDirectoryPerson Of(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new TestDirectoryPerson(user.ObjectId, user.Name, user.UserName, user.UserName);
    }
}
