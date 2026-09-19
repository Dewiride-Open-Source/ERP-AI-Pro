using System;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Domain;

public interface IStronglyTypedId<out TSelf>
    where TSelf : struct, IStronglyTypedId<TSelf>
{
    Guid Value { get; }

    static abstract TSelf Create();

    static abstract TSelf From(Guid value);
}
