using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.People.Domain;

internal readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>
{
    public static UserId Create() => new(Guid.CreateVersion7());

    public static UserId From(Guid value) => new(value);
}
