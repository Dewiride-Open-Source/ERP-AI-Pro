namespace Dewiride.Erp.Modules.Identity.Users.Departments.Domain;

// The parent of every department that is not deleted. A walk up from a department stops at a department it has seen already,
// so rows written outside the API that form a loop end the walk instead of looping.
internal sealed class DepartmentTree(IReadOnlyDictionary<DepartmentId, DepartmentId?> parents)
{
    public bool IsWithin(DepartmentId department, DepartmentId ancestor)
    {
        var seen = new HashSet<DepartmentId>();
        for (DepartmentId? current = department; current is { } id && seen.Add(id); current = parents.GetValueOrDefault(id))
        {
            if (id == ancestor)
            {
                return true;
            }
        }

        return false;
    }
}
