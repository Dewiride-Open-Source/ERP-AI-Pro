import assert from "node:assert/strict";
import { test } from "node:test";

import { opensPageSearch, pageSearchShortcutLabel, type ShortcutKeyEvent } from "./shortcut-keys.ts";

const press = (key: string, modifiers: Partial<Omit<ShortcutKeyEvent, "key">> = {}): ShortcutKeyEvent => ({
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
