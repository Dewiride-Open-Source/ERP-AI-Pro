import assert from "node:assert/strict";
import { test } from "node:test";

import { resolveStoredTheme } from "./stored-theme.ts";

test("resolveStoredTheme_ChosenLightOrDark_IsThatThemeWhateverTheSystemPrefers", () => {
  assert.equal(resolveStoredTheme("light", true), "light");
  assert.equal(resolveStoredTheme("dark", false), "dark");
});

test("resolveStoredTheme_SystemOrNothingChosen_FollowsTheSystem", () => {
  assert.equal(resolveStoredTheme("system", true), "dark");
  assert.equal(resolveStoredTheme("system", false), "light");
  assert.equal(resolveStoredTheme(null, true), "dark");
  assert.equal(resolveStoredTheme(null, false), "light");
});

test("resolveStoredTheme_UnknownValue_FollowsTheSystem", () => {
  assert.equal(resolveStoredTheme("Dark", false), "light");
  assert.equal(resolveStoredTheme("", true), "dark");
});
