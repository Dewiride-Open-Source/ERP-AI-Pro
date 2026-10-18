using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.Modules.Identity.Users.Contracts.People;

// For a module that brings people in from the company directory: it invites a person of the directory with the ERP roles it
// names, and records the positions they hold. Each operation keeps every rule of the route of this module that makes the same
// change and answers the same errors; it checks no permission, because the calling module answers for its own callers, and
// the change is made, and recorded, as the actor the caller runs under.
public interface IPeopleCommands
{
    Task<Result<Guid>> InvitePersonAsync(PersonInvitation invitation, CancellationToken cancellationToken);

    Task<Result<Guid>> RecordPositionAsync(PersonPosition position, CancellationToken cancellationToken);
}
