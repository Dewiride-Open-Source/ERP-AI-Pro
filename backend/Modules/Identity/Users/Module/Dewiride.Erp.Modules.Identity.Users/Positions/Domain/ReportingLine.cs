using Dewiride.Erp.Modules.Identity.Users.People.Domain;

namespace Dewiride.Erp.Modules.Identity.Users.Positions.Domain;

internal readonly record struct ReportingLine(UserId Person, DateOnly EffectiveFrom, UserId? Manager);
