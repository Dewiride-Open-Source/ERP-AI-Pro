namespace Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;

// For an endpoint whose request decides that it needs a permission beyond the one its route requires.
public interface ICallerPermissions
{
    bool Holds(string permission);
}
