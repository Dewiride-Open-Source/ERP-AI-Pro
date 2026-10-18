using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.InvitePerson;
using Dewiride.Erp.Modules.Identity.Users.Positions.Application.Commands.RecordPosition;

namespace Dewiride.Erp.Modules.Identity.Users.Integration;

// Each operation sends the command its route sends through the same handler pipeline, so the validation, the rules, the
// organisation lock, the unit of work and the change records are the route's own; what the route adds for its own callers,
// the permissions, the idempotency key and the read of the directory, stays with the module that calls.
internal sealed class PeopleCommands(
    ICommandHandler<InvitePersonCommand, Guid> invite,
    ICommandHandler<RecordPositionCommand, Guid> record) : IPeopleCommands
{
    public Task<Result<Guid>> InvitePersonAsync(PersonInvitation invitation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        return invite.HandleAsync(
            new InvitePersonCommand(invitation.EntraObjectId, invitation.DisplayName, invitation.SignInName, invitation.EmployeeCode, null, null, invitation.Roles),
            cancellationToken);
    }

    public Task<Result<Guid>> RecordPositionAsync(PersonPosition position, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(position);

        return record.HandleAsync(
            new RecordPositionCommand(position.PersonId, position.EffectiveFrom, position.DepartmentId, position.DesignationId, position.ManagerId),
            cancellationToken);
    }
}
