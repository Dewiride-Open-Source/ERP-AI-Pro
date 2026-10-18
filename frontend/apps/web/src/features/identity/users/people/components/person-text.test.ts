import assert from "node:assert/strict";
import { test } from "node:test";

import {
  deactivationNote,
  lastSignInText,
  notRecorded,
  roleLabel,
  rolesText,
  statusLabel,
} from "./person-text.ts";

test("statusLabel_EachStatus_IsTheWordOfTheFilter", () => {
  assert.equal(statusLabel("active"), "Active");
  assert.equal(statusLabel("deactivated"), "Deactivated");
});

test("statusLabel_NoStatus_IsNotRecorded", () => {
  assert.equal(statusLabel(undefined), notRecorded);
});

test("deactivationNote_EachReason_SaysWhoDeactivatedThePersonAndWhy", () => {
  assert.equal(deactivationNote("administrator"), "Deactivated by an administrator.");
  assert.equal(
    deactivationNote("accountDisabled"),
    "Deactivated by the account check, which found their Microsoft account disabled.",
  );
  assert.equal(
    deactivationNote("accountDeleted"),
    "Deactivated by the account check, which found their Microsoft account deleted.",
  );
});

test("deactivationNote_NoReason_SaysNothing", () => {
  assert.equal(deactivationNote(undefined), undefined);
});

test("roleLabel_EachBuiltInRole_HasItsPlainName", () => {
  assert.deepEqual((["employee", "finance", "hr", "manager", "administrator"] as const).map(roleLabel), [
    "Employee",
    "Finance",
    "HR",
    "Manager",
    "Administrator",
  ]);
});

test("rolesText_RolesInAnyOrder_NamesThemInTheOrderOfTheRoles", () => {
  assert.equal(rolesText(["manager", "finance"]), "Finance, Manager");
  assert.equal(rolesText(["hr", "manager", "finance"]), "Finance, HR, Manager");
  assert.equal(rolesText(["hr"]), "HR");
});

test("rolesText_NoRoles_SaysNone", () => {
  assert.equal(rolesText([]), "None");
});

test("lastSignInText_ASignIn_ShowsItsDateAndTimeInIndia", () => {
  assert.equal(lastSignInText(new Date("2026-10-17T07:55:00Z")), "17 Oct 2026, 1:25 pm");
});

test("lastSignInText_NoSignInYet_SaysNotSignedInYet", () => {
  assert.equal(lastSignInText(undefined), "Not signed in yet");
});
