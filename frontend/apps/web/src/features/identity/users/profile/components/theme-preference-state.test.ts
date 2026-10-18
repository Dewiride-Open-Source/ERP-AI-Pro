import assert from "node:assert/strict";
import { test } from "node:test";

import {
  isThemePreference,
  nextThemeStep,
  startKeeping,
  themePreferences,
  themeSaved,
  unsavedChoiceLifetimeMs,
  unsavedChoiceOf,
  unsavedChoiceText,
} from "./theme-preference-state.ts";

const now = Date.UTC(2026, 9, 18, 6, 30);

function watching(saved: "system" | "light" | "dark") {
  return nextThemeStep(startKeeping(saved, undefined), saved)[0];
}

test("nextThemeStep_BrowserShowsAnotherThemeThanTheAccountHolds_AppliesTheSavedThemeAndSavesNothing", () => {
  const [keeping, step] = nextThemeStep(startKeeping("dark", undefined), "light");

  assert.deepEqual(step, { kind: "apply", theme: "dark" });
  assert.deepEqual(keeping, { lastSaved: "dark", previous: undefined, unsaved: undefined });
});

test("nextThemeStep_EffectRunTwiceBeforeTheSavedThemeShows_AppliesItAgainAndSavesNothing", () => {
  const [once] = nextThemeStep(startKeeping("dark", undefined), "light");

  assert.deepEqual(nextThemeStep(once, "light")[1], { kind: "apply", theme: "dark" });
});

test("nextThemeStep_BrowserHoldsNoThemeOfItsOwn_AppliesTheSavedTheme", () => {
  assert.deepEqual(nextThemeStep(startKeeping("light", undefined), undefined)[1], {
    kind: "apply",
    theme: "light",
  });
});

test("nextThemeStep_SavedThemeShown_StartsWatchingWithoutSaving", () => {
  const [applied] = nextThemeStep(startKeeping("dark", undefined), "light");

  const [keeping, step] = nextThemeStep(applied, "dark");

  assert.deepEqual(step, { kind: "none" });
  assert.deepEqual(keeping, { lastSaved: "dark", previous: "dark", unsaved: undefined });
});

test("nextThemeStep_BrowserShowsAChoiceWhoseSaveNeverAnswered_SavesItInsteadOfApplyingTheSavedTheme", () => {
  const [keeping, step] = nextThemeStep(startKeeping("light", { theme: "dark", over: "light" }), "dark");

  assert.deepEqual(step, { kind: "save", theme: "dark" });
  assert.deepEqual(keeping, { lastSaved: "light", previous: "dark", unsaved: undefined });
});

test("nextThemeStep_UnsavedChoiceTheAccountHoldsAlready_SavesNothing", () => {
  assert.deepEqual(nextThemeStep(startKeeping("dark", { theme: "dark", over: "system" }), "dark")[1], {
    kind: "none",
  });
});

test("nextThemeStep_PageReloadedBeforeTheSaveWasRead_SavesTheNotedChoiceAgain", () => {
  const [keeping, step] = nextThemeStep(startKeeping("system", { theme: "dark", over: "system" }), "dark");

  assert.deepEqual(step, { kind: "save", theme: "dark" });
  assert.deepEqual(keeping, { lastSaved: "system", previous: "dark", unsaved: undefined });
});

test("startKeeping_AccountHoldsAThemeChosenSinceInAnotherBrowser_IgnoresTheNotedChoice", () => {
  const keeping = startKeeping("light", { theme: "dark", over: "system" });

  assert.equal(keeping.unsaved, undefined);
  assert.deepEqual(nextThemeStep(keeping, "dark")[1], { kind: "apply", theme: "light" });
});

test("nextThemeStep_BrowserShowsAnotherThemeThanTheUnsavedChoice_AppliesTheSavedTheme", () => {
  assert.deepEqual(nextThemeStep(startKeeping("light", { theme: "dark", over: "light" }), "system")[1], {
    kind: "apply",
    theme: "light",
  });
});

test("nextThemeStep_PersonChoosesAnotherTheme_SavesIt", () => {
  const [keeping, step] = nextThemeStep(watching("system"), "dark");

  assert.deepEqual(step, { kind: "save", theme: "dark" });
  assert.deepEqual(keeping, { lastSaved: "system", previous: "dark", unsaved: undefined });
});

test("nextThemeStep_SameThemeSeenAgain_DoesNothing", () => {
  const [chosen] = nextThemeStep(watching("system"), "dark");

  assert.deepEqual(nextThemeStep(chosen, "dark")[1], { kind: "none" });
  assert.deepEqual(nextThemeStep(chosen, undefined)[1], { kind: "none" });
});

test("nextThemeStep_ChoiceBackToTheThemeTheAccountHolds_SavesNothing", () => {
  const [chosen] = nextThemeStep(watching("system"), "dark");

  assert.deepEqual(nextThemeStep(chosen, "system")[1], { kind: "none" });
});

test("nextThemeStep_ChoiceAfterASaveThatFailed_SavesTheNewChoice", () => {
  const [unsaved] = nextThemeStep(watching("system"), "dark");

  assert.deepEqual(nextThemeStep(unsaved, "light")[1], { kind: "save", theme: "light" });
});

test("themeSaved_ChoiceSavedThenAnotherThatFailed_SavesNothingWhenTheSavedOneIsChosenAgain", () => {
  const [chosen] = nextThemeStep(watching("system"), "dark");
  const saved = themeSaved(chosen, "dark");
  const [unsaved] = nextThemeStep(saved, "light");

  assert.deepEqual(saved, { lastSaved: "dark", previous: "dark", unsaved: undefined });
  assert.deepEqual(nextThemeStep(unsaved, "dark")[1], { kind: "none" });
});

test("unsavedChoiceOf_ChoiceNotedWithinItsLifetime_IsThatChoiceAndTheThemeItReplaced", () => {
  const dark = { theme: "dark", over: "system" } as const;
  const light = { theme: "light", over: "dark" } as const;

  assert.deepEqual(unsavedChoiceOf(unsavedChoiceText(dark, now), now), dark);
  assert.deepEqual(unsavedChoiceOf(unsavedChoiceText(light, now - unsavedChoiceLifetimeMs), now), light);
});

test("unsavedChoiceOf_ChoiceNotedLongAgoOrLater_IsNone", () => {
  const dark = { theme: "dark", over: "system" } as const;

  assert.equal(unsavedChoiceOf(unsavedChoiceText(dark, now - unsavedChoiceLifetimeMs - 1), now), undefined);
  assert.equal(unsavedChoiceOf(unsavedChoiceText(dark, now + 1), now), undefined);
});

test("unsavedChoiceOf_NothingOrSomethingElseStored_IsNone", () => {
  for (const stored of [
    null,
    "",
    "dark",
    "{",
    "null",
    "[]",
    '{"theme":"sepia","chosenAt":0}',
    '{"theme":"dark"}',
    `{"theme":"dark","chosenAt":${now}}`,
    `{"theme":"dark","over":"sepia","chosenAt":${now}}`,
  ]) {
    assert.equal(unsavedChoiceOf(stored, now), undefined, String(stored));
  }
});

test("isThemePreference_EachThemeOfTheApi_IsATheme", () => {
  for (const theme of themePreferences) assert.equal(isThemePreference(theme), true);
  for (const value of [undefined, "", "Dark", "sepia", 1]) assert.equal(isThemePreference(value), false);
});

test("themePreferences_EveryThemeOfTheApi_InTheOrderTheChooserOffersThem", () => {
  assert.deepEqual(themePreferences, ["system", "light", "dark"]);
});
