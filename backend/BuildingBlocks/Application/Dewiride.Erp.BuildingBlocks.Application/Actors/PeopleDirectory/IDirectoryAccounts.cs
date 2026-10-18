using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;

// The accounts of the company directory in Microsoft Entra, read with the ERP's own application permission rather than on
// behalf of a person, so reading them needs nobody signed in. Every object id asked about has a state in the answer: an
// account the directory does not hold is deleted.
public interface IDirectoryAccounts
{
    Task<Result<IReadOnlyDictionary<Guid, DirectoryAccountState>>> StatesOfAsync(IReadOnlyCollection<Guid> objectIds, CancellationToken cancellationToken);
}
