using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

public static class DirectoryErrors
{
    public static readonly Error PersonNotFound = Error.NotFound("directory.person-not-found", "The company directory holds no person with this Entra object id.");

    public static readonly Error AccessDenied = Error.Forbidden("directory.access-denied", "Microsoft Entra does not let the ERP read the company directory on your behalf; signing in again, or consent for the whole organisation, may be needed.");

    public static readonly Error Unavailable = Error.Unavailable("directory.unavailable", "The company directory cannot be read right now; try again in a moment.");
}
