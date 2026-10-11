import assert from "node:assert/strict";
import { test } from "node:test";

import type { Permission } from "@dewiride/erp-api-client";

import type { HeldSession } from "../../auth/session.ts";

import { visibleEntries } from "./visible-entries.ts";

const systemInfo = {
  id: "platform-system-info",
  featureFlag: "Erp.Modules.Platform.SystemInfo",
  permission: "platform.system-info.info.read",
} as const;

const attachments = {
  id: "platform-attachments",
  featureFlag: "Erp.Modules.Platform.Attachments",
  permission: "platform.attachments.files.read",
} as const;

const navigation = [systemInfo, attachments];

const everyFlagUnreported = new Map<string, boolean>();

function signedIn(permissions: readonly Permission[]): HeldSession {
  return {
    status: "signed-in",
    person: {
      id: "5c1f9e2a-7d3b-4f6e-9a8c-2b4d6f8a0c1e",
      name: "Asha Rao",
      userName: "asha.rao@dewiride.example",
      roles: [],
      permissions,
    },
  };
}

const unavailable: HeldSession = { status: "unavailable", failure: new Error("The API did not respond.") };

function ids(entries: readonly { readonly id: string }[]): readonly string[] {
  return entries.map((entry) => entry.id);
}

test("visibleEntries_SignedInPersonHoldingEveryEntrysPermission_KeepsEveryEntryInItsOrder", () => {
  const session = signedIn(["platform.attachments.files.read", "platform.system-info.info.read"]);

  assert.deepEqual(ids(visibleEntries(navigation, everyFlagUnreported, session)), [
    "platform-system-info",
    "platform-attachments",
  ]);
});

test("visibleEntries_SignedInPersonLackingAnEntrysPermission_LeavesThatEntryOut", () => {
  const session = signedIn(["platform.attachments.files.read", "platform.attachments.files.upload"]);

  assert.deepEqual(ids(visibleEntries(navigation, everyFlagUnreported, session)), ["platform-attachments"]);
});

test("visibleEntries_SignedInPersonWithoutPermissions_KeepsNoEntry", () => {
  assert.deepEqual(ids(visibleEntries(navigation, everyFlagUnreported, signedIn([]))), []);
});

test("visibleEntries_SessionUnavailable_KeepsEveryEntryWhoseFlagIsEnabled", () => {
  const flags = new Map([
    ["Erp.Modules.Platform.SystemInfo", true],
    ["Erp.Modules.Platform.Attachments", true],
  ]);

  assert.deepEqual(ids(visibleEntries(navigation, flags, unavailable)), [
    "platform-system-info",
    "platform-attachments",
  ]);
});

test("visibleEntries_FlagDisabled_LeavesTheEntryOutWhateverTheSession", () => {
  const flags = new Map([["Erp.Modules.Platform.SystemInfo", false]]);
  const administrator = signedIn(["platform.attachments.files.read", "platform.system-info.info.read"]);

  assert.deepEqual(ids(visibleEntries(navigation, flags, administrator)), ["platform-attachments"]);
  assert.deepEqual(ids(visibleEntries(navigation, flags, unavailable)), ["platform-attachments"]);
});
