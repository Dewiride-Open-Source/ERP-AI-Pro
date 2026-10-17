import assert from "node:assert/strict";
import { test } from "node:test";

import { permissionMatrix, roleLabels } from "./permission-rows.ts";

const roles = ["employee", "finance", "hr", "manager", "administrator"] as const;

test("permissionMatrix_RolesOfTheCatalogue_AreTheColumnsInItsOrderWithTheirLabels", () => {
  const { columns } = permissionMatrix({ roles: [...roles], permissions: [] });

  assert.deepEqual(columns, [
    { role: "employee", label: "Employee" },
    { role: "finance", label: "Finance" },
    { role: "hr", label: "HR" },
    { role: "manager", label: "Manager" },
    { role: "administrator", label: "Administrator" },
  ]);
});

test("permissionMatrix_Permission_MarksEveryRoleAsHeldOrNot", () => {
  const { rows } = permissionMatrix({
    roles: [...roles],
    permissions: [
      {
        name: "identity.users.departments.read",
        description: "See the departments and which department each one sits under.",
        heldBy: ["employee", "administrator"],
      },
    ],
  });

  assert.deepEqual(rows, [
    {
      name: "identity.users.departments.read",
      description: "See the departments and which department each one sits under.",
      cells: [
        { role: "employee", held: true },
        { role: "finance", held: false },
        { role: "hr", held: false },
        { role: "manager", held: false },
        { role: "administrator", held: true },
      ],
    },
  ]);
});

test("permissionMatrix_Permissions_KeepTheOrderOfTheCatalogue", () => {
  const { rows } = permissionMatrix({
    roles: ["administrator"],
    permissions: [
      {
        name: "platform.audit.changes.export",
        description: "Download the audit trail.",
        heldBy: ["administrator"],
      },
      { name: "identity.users.people.read", description: "See the people.", heldBy: ["administrator"] },
    ],
  });

  assert.deepEqual(
    rows.map((row) => row.name),
    ["platform.audit.changes.export", "identity.users.people.read"],
  );
});

test("permissionMatrix_PermissionWithoutANameOrADescription_IsLeftOut", () => {
  const { rows } = permissionMatrix({
    roles: ["administrator"],
    permissions: [
      { name: "identity.users.people.read", description: "See the people.", heldBy: ["administrator"] },
      { name: "identity.users.people.update", description: null, heldBy: ["administrator"] },
      { description: "Register a person.", heldBy: ["administrator"] },
      { name: "", description: "Delete a person's record.", heldBy: ["administrator"] },
    ],
  });

  assert.deepEqual(
    rows.map((row) => row.name),
    ["identity.users.people.read"],
  );
});

test("permissionMatrix_PermissionWithoutHolders_IsHeldByNoRole", () => {
  const { rows } = permissionMatrix({
    roles: ["employee", "administrator"],
    permissions: [{ name: "identity.users.people.read", description: "See the people.", heldBy: null }],
  });

  assert.deepEqual(rows[0]?.cells, [
    { role: "employee", held: false },
    { role: "administrator", held: false },
  ]);
});

test("permissionMatrix_RoleListedTwice_IsOneColumn", () => {
  const { columns } = permissionMatrix({ roles: ["employee", "employee", "administrator"], permissions: [] });

  assert.deepEqual(
    columns.map((column) => column.role),
    ["employee", "administrator"],
  );
});

test("permissionMatrix_CatalogueWithoutRolesOrPermissions_IsEmpty", () => {
  assert.deepEqual(permissionMatrix({ roles: null, permissions: null }), { columns: [], rows: [] });
  assert.deepEqual(permissionMatrix({}), { columns: [], rows: [] });
});

test("roleLabels_EveryBuiltInRole_HasItsPlainName", () => {
  assert.deepEqual(
    roles.map((role) => roleLabels[role]),
    ["Employee", "Finance", "HR", "Manager", "Administrator"],
  );
});
