namespace Dewiride.Erp.BuildingBlocks.Application.Actors;

// Every session starts with AdmitAsync, which brings the person's record up to date from the sign-in, and every request of a
// session or of a person's bearer token asks IsAdmittedAsync, so a person an administrator deactivates or deletes is refused
// at once instead of when the session ends.
public interface IPersonAdmission
{
    Task<bool> AdmitAsync(SignedInPerson person, CancellationToken cancellationToken);

    Task<bool> IsAdmittedAsync(Guid objectId, CancellationToken cancellationToken);
}
