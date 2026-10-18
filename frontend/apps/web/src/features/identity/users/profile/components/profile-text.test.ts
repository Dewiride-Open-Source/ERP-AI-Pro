import assert from "node:assert/strict";
import { test } from "node:test";

import { notRecorded, recordedText, roleNames } from "./profile-text.ts";

test("recordedText_ValueAnAdministratorRecorded_ShowsItTrimmed", () => {
  assert.equal(recordedText(" DW-0042 "), "DW-0042");
});

test("recordedText_NothingRecorded_SaysSo", () => {
  for (const value of [undefined, null, "", "   "]) assert.equal(recordedText(value), notRecorded);
});

test("roleNames_RolesInTheOrderTheApiListsThem_NamesEachInPlainWords", () => {
  assert.deepEqual(roleNames(["employee", "hr", "manager", "administrator"]), [
    "Employee",
    "HR",
    "Manager",
    "Administrator",
  ]);
});

test("roleNames_NoRolesAnswered_NamesNone", () => {
  assert.deepEqual(roleNames(undefined), []);
  assert.deepEqual(roleNames(null), []);
});
