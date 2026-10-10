namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

public sealed record DirectorySearch(IReadOnlyList<DirectoryPerson> People, bool HasMore)
{
    public const int MinTextLength = 2;

    public const int MaxTextLength = 100;

    public const int MaxPeople = 25;
}
