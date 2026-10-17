namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

// A record of who did something keeps only the person's Microsoft Entra object id, so a reader of the records asks for their
// names when it shows them; an object id without a name is left out of the answer.
public interface IPersonNames
{
    Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken);
}
