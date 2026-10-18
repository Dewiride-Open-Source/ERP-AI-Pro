import assert from "node:assert/strict";
import { test } from "node:test";

import { orderedRoles, withRole } from "./role-selection.ts";

test("orderedRoles_RolesInAnyOrder_ListsThemAsTheErpDoes", () => {
  assert.deepEqual(orderedRoles(["manager", "finance"]), ["finance", "manager"]);
  assert.deepEqual(orderedRoles([]), []);
});

test("withRole_RoleTickedOrCleared_ChangesOnlyThatRoleAndKeepsTheOrder", () => {
  assert.deepEqual(withRole(["manager"], "finance", true), ["finance", "manager"]);
  assert.deepEqual(withRole(["finance", "manager"], "finance", false), ["manager"]);
  assert.deepEqual(withRole(["hr"], "hr", true), ["hr"]);
  assert.deepEqual(withRole([], "manager", false), []);
});
