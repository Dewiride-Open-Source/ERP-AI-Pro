using Dewiride.Erp.BuildingBlocks.Application.Queries;

namespace Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.ListActiveAccounts;

internal sealed record ListActiveAccountsQuery : IQuery<IReadOnlyList<Guid>>;
