namespace Dewiride.Erp.Modules.Identity.Users.Contracts.Positions;

public sealed record ReportingManagerEntry(Guid PersonId, Guid? ObjectId, bool IsActive);
