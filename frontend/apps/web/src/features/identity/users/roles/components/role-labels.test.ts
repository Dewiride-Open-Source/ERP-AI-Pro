import assert from "node:assert/strict";
import { test } from "node:test";

import { orderedRoles, roleLabels, withRole } from "./role-labels.ts";

test("roleLabels_EveryAssignableRole_HasItsName", () => {
  assert.deepEqual(roleLabels, { finance: "Finance", hr: "HR", manager: "Manager" });
});

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
