namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints;

internal sealed record TestDepartment(Guid Id, string Name, Guid? ParentId, string Version);
