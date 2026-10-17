using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;

namespace Dewiride.Erp.BuildingBlocks.Modules;

// Administrator holds every permission, so a permission a module adds is an administrator's alone until the module names
// other roles for it.
public sealed partial class PermissionCatalog
{
    private readonly FrozenDictionary<string, FrozenSet<ErpRole>> _holders;

    private readonly FrozenSet<string> _all;

    public PermissionCatalog(ModuleCatalog modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var declarations = new Dictionary<string, PermissionDeclaration>(StringComparer.Ordinal);
        foreach (var descriptor in modules.Modules.Select(m => m.Descriptor))
        {
            foreach (var permission in descriptor.Permissions)
            {
                if (!PermissionName().IsMatch(permission.Name))
                {
                    throw new InvalidOperationException($"Module '{descriptor.Id}' declares the permission '{permission.Name}', which is not four lower-case segments <domain>.<module>.<feature>.<action>.");
                }

                if (permission.HeldBy.Contains(ErpRole.Administrator))
                {
                    throw new InvalidOperationException($"Module '{descriptor.Id}' names {ErpRole.Administrator} for the permission '{permission.Name}', which holds every permission.");
                }

                if (string.IsNullOrWhiteSpace(permission.Description))
                {
                    throw new InvalidOperationException($"Module '{descriptor.Id}' declares the permission '{permission.Name}' without a description of what a holder may do.");
                }

                if (!declarations.TryAdd(permission.Name, permission))
                {
                    throw new InvalidOperationException($"The permission '{permission.Name}' is declared more than once.");
                }
            }
        }

        _holders = declarations.ToFrozenDictionary(declaration => declaration.Key, declaration => declaration.Value.HeldBy.ToFrozenSet(), StringComparer.Ordinal);
        _all = _holders.Keys.ToFrozenSet(StringComparer.Ordinal);
        Permissions = [.. declarations.Values.OrderBy(declaration => declaration.Name, StringComparer.Ordinal).Select(EntryOf)];
        Names = [.. Permissions.Select(permission => permission.Name)];
    }

    public IReadOnlyList<string> Names { get; }

    public IReadOnlyList<PermissionCatalogEntry> Permissions { get; }

    public bool Contains(string permission) => _holders.ContainsKey(permission);

    public IReadOnlySet<string> PermissionsOf(IEnumerable<ErpRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var held = roles.ToHashSet();

        return held.Contains(ErpRole.Administrator)
            ? _all
            : _holders.Where(permission => permission.Value.Overlaps(held)).Select(permission => permission.Key).ToFrozenSet(StringComparer.Ordinal);
    }

    private static PermissionCatalogEntry EntryOf(PermissionDeclaration declaration) =>
        new(
            declaration.Name,
            declaration.Description,
            [.. Enum.GetValues<ErpRole>().Where(role => role == ErpRole.Administrator || declaration.HeldBy.Contains(role))]);

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*){3}$")]
    private static partial Regex PermissionName();
}
