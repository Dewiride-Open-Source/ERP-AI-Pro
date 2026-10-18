using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Application.Queries.ListTeam;

internal sealed record ListTeamQuery(Guid PersonId) : IQuery<IReadOnlyList<TeamMemberDetails>>;
