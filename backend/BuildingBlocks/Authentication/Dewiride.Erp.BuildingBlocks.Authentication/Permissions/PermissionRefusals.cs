using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Authentication.Permissions;

internal static class PermissionRefusals
{
    private static readonly object RefusedItem = new();

    public static void Mark(HttpContext context) => context.Items[RefusedItem] = true;

    public static bool IsMarked(HttpContext context) => context.Items.ContainsKey(RefusedItem);
}
