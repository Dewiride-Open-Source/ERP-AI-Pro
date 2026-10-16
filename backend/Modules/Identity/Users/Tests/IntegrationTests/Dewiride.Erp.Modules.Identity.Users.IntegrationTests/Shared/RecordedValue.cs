using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;

internal sealed record RecordedValue(string Property, ChangeValueKind Kind, string? Before, string? After, string? Reference, string? BeforeName, string? AfterName);
