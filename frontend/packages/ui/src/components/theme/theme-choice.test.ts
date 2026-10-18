import assert from "node:assert/strict";
import { test } from "node:test";

import { isThemeChoice, themeChoices } from "./theme-choice.ts";

test("isThemeChoice_EachThemeOfTheProvider_IsAChoice", () => {
  for (const choice of themeChoices) assert.equal(isThemeChoice(choice), true);
});

test("isThemeChoice_AnythingElseTheStorageMayHold_IsNoChoice", () => {
  for (const value of [undefined, null, "", "Dark", "sepia", " light", 1])
    assert.equal(isThemeChoice(value), false);
});
