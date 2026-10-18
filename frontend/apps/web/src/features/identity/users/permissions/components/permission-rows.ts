import type { ErpRole, PermissionCatalogResponse } from "@dewiride/erp-api-client";

import { roleLabel } from "../../people/components/person-text.ts";

export interface RoleColumn {
  readonly role: ErpRole;
  readonly label: string;
}

export interface PermissionCell {
  readonly role: ErpRole;
  readonly held: boolean;
}

export interface PermissionRow {
  readonly name: string;
  readonly description: string;
  readonly cells: readonly PermissionCell[];
}

export interface PermissionMatrix {
  readonly columns: readonly RoleColumn[];
  readonly rows: readonly PermissionRow[];
}

export type PermissionCatalog = Pick<PermissionCatalogResponse, "roles" | "permissions">;

// The columns are the roles the answer lists, in its order, so a role that holds nothing yet still has its column.
export function permissionMatrix(catalog: PermissionCatalog): PermissionMatrix {
  const columns = [...new Set(catalog.roles ?? [])].map((role) => ({ role, label: roleLabel(role) }));
  const rows = (catalog.permissions ?? []).flatMap((permission) => {
    if (!permission.name || !permission.description) return [];
    const holders = new Set(permission.heldBy ?? []);
    return [
      {
        name: permission.name,
        description: permission.description,
        cells: columns.map(({ role }) => ({ role, held: holders.has(role) })),
      },
    ];
  });
  return { columns, rows };
}
