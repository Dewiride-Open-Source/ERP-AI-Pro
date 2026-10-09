import assert from "node:assert/strict";
import { test } from "node:test";

import {
  opensPageSearch,
  pageSearchShortcutLabel,
  togglesNavigation,
  type ShortcutKeyEvent,
} from "./shortcut-keys.ts";

const press = (
  key: string | undefined,
  modifiers: Partial<Omit<ShortcutKeyEvent, "key">> = {},
): ShortcutKeyEvent => ({
  key,
  ctrlKey: false,
  metaKey: false,
  altKey: false,
  shiftKey: false,
  ...modifiers,
});

test("opensPageSearch_ControlOrCommandWithK_Opens", () => {
  assert.equal(opensPageSearch(press("k", { ctrlKey: true })), true);
  assert.equal(opensPageSearch(press("k", { metaKey: true })), true);
  assert.equal(opensPageSearch(press("K", { ctrlKey: true })), true);
});

test("opensPageSearch_KWithoutTheModifierOrWithAnotherOne_DoesNotOpen", () => {
  assert.equal(opensPageSearch(press("k")), false);
  assert.equal(opensPageSearch(press("k", { ctrlKey: true, shiftKey: true })), false);
  assert.equal(opensPageSearch(press("k", { metaKey: true, altKey: true })), false);
  assert.equal(opensPageSearch(press("b", { ctrlKey: true })), false);
});

test("opensPageSearch_KeyHeldDown_CountsOnlyTheFirstPress", () => {
  assert.equal(opensPageSearch(press("k", { ctrlKey: true, repeat: false })), true);
  assert.equal(opensPageSearch(press("k", { ctrlKey: true, repeat: true })), false);
});

test("opensPageSearch_KeydownWithoutAKey_DoesNotOpen", () => {
  assert.equal(opensPageSearch(press(undefined, { ctrlKey: true })), false);
});

test("togglesNavigation_ControlOrCommandWithB_IsTheSidebarShortcut", () => {
  assert.equal(togglesNavigation(press("b", { ctrlKey: true })), true);
  assert.equal(togglesNavigation(press("b", { metaKey: true })), true);
  assert.equal(togglesNavigation(press("b")), false);
  assert.equal(togglesNavigation(press("k", { ctrlKey: true })), false);
  assert.equal(togglesNavigation(press(undefined, { ctrlKey: true })), false);
});

test("togglesNavigation_KeyHeldDown_KeepsMatchingWhatTheSidebarHears", () => {
  assert.equal(togglesNavigation(press("b", { ctrlKey: true, repeat: true })), true);
});

test("togglesNavigation_CapitalB_IsNotTheSidebarShortcut", () => {
  assert.equal(togglesNavigation(press("B", { ctrlKey: true, shiftKey: true })), false);
});

test("pageSearchShortcutLabel_ApplePlatforms_NameTheCommandKey", () => {
  assert.equal(
    pageSearchShortcutLabel("Mozilla/5.0 (Macintosh; Intel Mac OS X 15_6) AppleWebKit/605.1.15"),
    "⌘K",
  );
  assert.equal(pageSearchShortcutLabel("Mozilla/5.0 (iPhone; CPU iPhone OS 26_0 like Mac OS X)"), "⌘K");
});

test("pageSearchShortcutLabel_OtherPlatformsOrNoBrowser_NameControl", () => {
  assert.equal(
    pageSearchShortcutLabel("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"),
    "Ctrl K",
  );
  assert.equal(pageSearchShortcutLabel("Mozilla/5.0 (Linux; Android 16; Pixel 10)"), "Ctrl K");
  assert.equal(pageSearchShortcutLabel(""), "Ctrl K");
});
