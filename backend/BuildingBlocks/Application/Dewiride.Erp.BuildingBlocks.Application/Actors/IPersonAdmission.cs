namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

// Every session starts with AdmitAsync, which brings the person's record up to date from the sign-in, and every request of a
// session or of a person's bearer token asks AdmissionOfAsync, so a person an administrator deactivates or deletes is refused
// at once instead of when the session ends, and the roles an administrator assigns them count from their next request.
public interface IPersonAdmission
{
    Task<bool> AdmitAsync(SignedInPerson person, CancellationToken cancellationToken);

    Task<PersonAdmission?> AdmissionOfAsync(Guid objectId, CancellationToken cancellationToken);
}
