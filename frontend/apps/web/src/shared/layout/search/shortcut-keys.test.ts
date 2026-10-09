import assert from "node:assert/strict";
import { test } from "node:test";

import {
  isNavigationShortcut,
  isPageSearchShortcut,
  pageSearchShortcutLabel,
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

const heldDown = (event: ShortcutKeyEvent): ShortcutKeyEvent => {
  const repeated = { ...event, repeat: true };
  return repeated;
};

test("isPageSearchShortcut_ControlOrCommandWithK_Matches", () => {
  assert.equal(isPageSearchShortcut(press("k", { ctrlKey: true })), true);
  assert.equal(isPageSearchShortcut(press("k", { metaKey: true })), true);
  assert.equal(isPageSearchShortcut(press("K", { ctrlKey: true })), true);
});

test("isPageSearchShortcut_KWithoutTheModifierOrWithAnotherOne_DoesNotMatch", () => {
  assert.equal(isPageSearchShortcut(press("k")), false);
  assert.equal(isPageSearchShortcut(press("k", { ctrlKey: true, shiftKey: true })), false);
  assert.equal(isPageSearchShortcut(press("k", { metaKey: true, altKey: true })), false);
  assert.equal(isPageSearchShortcut(press("b", { ctrlKey: true })), false);
});

test("isPageSearchShortcut_KeyHeldDown_StillMatches", () => {
  assert.equal(isPageSearchShortcut(heldDown(press("k", { ctrlKey: true }))), true);
});

test("isPageSearchShortcut_KeydownWithoutAKey_DoesNotMatch", () => {
  assert.equal(isPageSearchShortcut(press(undefined, { ctrlKey: true })), false);
});

test("isNavigationShortcut_ControlOrCommandWithB_IsTheSidebarShortcut", () => {
  assert.equal(isNavigationShortcut(press("b", { ctrlKey: true })), true);
  assert.equal(isNavigationShortcut(press("b", { metaKey: true })), true);
  assert.equal(isNavigationShortcut(press("b")), false);
  assert.equal(isNavigationShortcut(press("k", { ctrlKey: true })), false);
  assert.equal(isNavigationShortcut(press(undefined, { ctrlKey: true })), false);
});

test("isNavigationShortcut_KeyHeldDown_StillMatches", () => {
  assert.equal(isNavigationShortcut(heldDown(press("b", { ctrlKey: true }))), true);
});

test("isNavigationShortcut_CapitalB_IsNotTheSidebarShortcut", () => {
  assert.equal(isNavigationShortcut(press("B", { ctrlKey: true, shiftKey: true })), false);
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
