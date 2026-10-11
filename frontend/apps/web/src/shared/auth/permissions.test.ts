import assert from "node:assert/strict";
import { test } from "node:test";

import { holdsPermission, mayShow } from "./permissions.ts";
import type { HeldSession, SignedInPerson } from "./session.ts";

const employee: SignedInPerson = {
  id: "5c1f9e2a-7d3b-4f6e-9a8c-2b4d6f8a0c1e",
  name: "Asha Rao",
  userName: "asha.rao@dewiride.example",
  roles: ["Erp.User"],
  permissions: ["platform.attachments.files.read", "platform.attachments.files.upload"],
};

const signedIn: HeldSession = { status: "signed-in", person: employee };

const unavailable: HeldSession = { status: "unavailable", failure: new Error("The API did not respond.") };

test("holdsPermission_PermissionAmongThePersonsPermissions_ReturnsTrue", () => {
  assert.equal(holdsPermission(employee, "platform.attachments.files.read"), true);
  assert.equal(holdsPermission(employee, "platform.attachments.files.upload"), true);
});

test("holdsPermission_PermissionThePersonLacks_ReturnsFalse", () => {
  assert.equal(holdsPermission(employee, "platform.attachments.files.delete"), false);
  assert.equal(holdsPermission(employee, "platform.system-info.info.read"), false);
});

test("holdsPermission_PersonWithoutPermissions_ReturnsFalse", () => {
  assert.equal(holdsPermission({ permissions: [] }, "platform.attachments.files.read"), false);
});

test("mayShow_SignedInPersonHoldingThePermission_ReturnsTrue", () => {
  assert.equal(mayShow(signedIn, "platform.attachments.files.upload"), true);
});

test("mayShow_SignedInPersonLackingThePermission_ReturnsFalse", () => {
  assert.equal(mayShow(signedIn, "platform.attachments.files.delete"), false);
});

test("mayShow_SessionUnavailable_ReturnsTrueWhateverThePermission", () => {
  assert.equal(mayShow(unavailable, "platform.attachments.files.delete"), true);
  assert.equal(mayShow(unavailable, "identity.users.people.assign-roles"), true);
});
