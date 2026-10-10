using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

// The company directory in Microsoft Entra, read on behalf of the person signed in to the request, so it shows them only
// what Entra lets them read and only a request of a signed-in person can read it.
public interface IPeopleDirectory
{
    Task<Result<DirectorySearch>> SearchAsync(string text, CancellationToken cancellationToken);

    Task<Result<DirectoryPerson>> FindAsync(Guid objectId, CancellationToken cancellationToken);
}
