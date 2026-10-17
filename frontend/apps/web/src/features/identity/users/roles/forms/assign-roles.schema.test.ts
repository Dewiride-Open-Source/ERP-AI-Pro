import assert from "node:assert/strict";
import { test } from "node:test";

import { assignRolesProblemMessages, assignRolesSchema, assignRolesValues } from "./assign-roles.schema.ts";

const personId = "0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e";

test("assignRolesValues_RolesOfTheRecord_StartTheFormWithACopyOfThem", () => {
  const roles = ["hr"] as const;
  const values = assignRolesValues(personId, roles, "AAAAAAAAB9E=");

  assert.deepEqual(values, { personId, roles: ["hr"], version: "AAAAAAAAB9E=" });
  assert.notEqual(values.roles, roles);
});

test("assignRolesSchema_ChosenRoles_ParsesThemWithTheRecordAndItsVersion", () => {
  assert.deepEqual(
    assignRolesSchema.parse({ personId, roles: ["finance", "manager"], version: "AAAAAAAAB9E=" }),
    { personId, roles: ["finance", "manager"], version: "AAAAAAAAB9E=" },
  );
  assert.deepEqual(assignRolesSchema.parse({ personId, roles: [], version: "AAAAAAAAB9E=" }).roles, []);
});

test("assignRolesSchema_RoleTheErpDoesNotAssign_IsRefused", () => {
  const result = assignRolesSchema.safeParse({ personId, roles: ["administrator"], version: "AAAAAAAAB9E=" });

  assert.equal(result.success, false);
  assert.deepEqual(
    result.error?.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`),
    ["roles.0: Choose roles from the list."],
  );
});

test("assignRolesProblemMessages_RecordChangedMeanwhile_AsksForAReload", () => {
  assert.equal(
    assignRolesProblemMessages["concurrency.conflict"],
    "Someone else changed this record after the page was loaded. Reload the page, then assign the roles again.",
  );
});
