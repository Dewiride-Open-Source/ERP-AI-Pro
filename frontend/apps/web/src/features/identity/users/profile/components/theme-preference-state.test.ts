import assert from "node:assert/strict";
import { test } from "node:test";

import {
  accountRead,
  isSameChoice,
  isThemePreference,
  nextThemeStep,
  startKeeping,
  themeAnswered,
  themePreferences,
  unsavedChoiceLifetimeMs,
  unsavedChoiceOf,
  unsavedChoiceText,
  type AccountTheme,
  type ThemeKeeping,
  type UnsavedChoice,
} from "./theme-preference-state.ts";

const now = Date.UTC(2026, 9, 18, 6, 30);

const meera = "0199f3a4-5b6c-7d8e-9f01-23456789abcd";

const arjun = "0199f3a4-5b6c-7d8e-9f01-3456789abcde";

const systemAccount: AccountTheme = { theme: "system", choiceId: null };

const darkAccount: AccountTheme = { theme: "dark", choiceId: "8a1f0c2e-7b3d-8e4f-9a5b-6c7d8e9f0a1b" };

const lightAccount: AccountTheme = { theme: "light", choiceId: "8b2e1d3f-6c4a-8f5e-8b6c-7d8e9f0a1b2c" };

function noted(theme: "system" | "light" | "dark", over: string | null, person = meera): UnsavedChoice {
  return { person, theme, over };
}

function watching(account: AccountTheme): ThemeKeeping {
  return nextThemeStep(startKeeping(meera, account, undefined), account.theme)[0];
}

test("nextThemeStep_BrowserShowsAnotherThemeThanTheAccountHolds_AppliesTheAccountsThemeAndSavesNothing", () => {
  const [keeping, step] = nextThemeStep(startKeeping(meera, darkAccount, undefined), "light");

  assert.deepEqual(step, { kind: "apply", theme: "dark" });
  assert.equal(keeping.previous, undefined);
});

test("nextThemeStep_EffectRunTwiceBeforeTheAccountsThemeShows_AppliesItAgainAndSavesNothing", () => {
  const [once] = nextThemeStep(startKeeping(meera, darkAccount, undefined), "light");

  assert.deepEqual(nextThemeStep(once, "light")[1], { kind: "apply", theme: "dark" });
});

test("nextThemeStep_BrowserHoldsNoThemeOfItsOwn_AppliesTheAccountsTheme", () => {
  assert.deepEqual(nextThemeStep(startKeeping(meera, lightAccount, undefined), undefined)[1], {
    kind: "apply",
    theme: "light",
  });
});

test("nextThemeStep_AccountsThemeShown_StartsWatchingWithoutSaving", () => {
  const [applied] = nextThemeStep(startKeeping(meera, darkAccount, undefined), "light");

  const [keeping, step] = nextThemeStep(applied, "dark");

  assert.deepEqual(step, { kind: "none" });
  assert.equal(keeping.previous, "dark");
});

test("nextThemeStep_PageReloadedBeforeTheSaveArrived_SavesTheNotedChoiceAgain", () => {
  const found = noted("dark", null);

  const [keeping, step] = nextThemeStep(startKeeping(meera, systemAccount, found), "dark");

  assert.deepEqual(step, { kind: "send", choice: found });
  assert.equal(keeping.sending, "dark");
  assert.deepEqual(keeping.noted, found);
});

test("nextThemeStep_NotedChoiceTheAccountHoldsAlready_ForgetsItAndSavesNothing", () => {
  const found = noted("dark", darkAccount.choiceId);

  assert.deepEqual(nextThemeStep(startKeeping(meera, darkAccount, found), "dark")[1], {
    kind: "forget",
    choice: found,
  });
});

test("nextThemeStep_BrowserShowsAnotherThemeThanTheNotedChoice_AppliesTheAccountsThemeThenForgetsTheNote", () => {
  const found = noted("dark", lightAccount.choiceId);
  const [applied, step] = nextThemeStep(startKeeping(meera, lightAccount, found), "system");

  assert.deepEqual(step, { kind: "apply", theme: "light" });
  assert.deepEqual(nextThemeStep(applied, "light")[1], { kind: "forget", choice: found });
});

test("startKeeping_AccountChoseSinceInAnotherBrowser_IgnoresTheNotedChoice", () => {
  const keeping = startKeeping(meera, darkAccount, noted("light", lightAccount.choiceId));

  assert.equal(keeping.noted, undefined);
  assert.deepEqual(nextThemeStep(keeping, "light")[1], { kind: "apply", theme: "dark" });
});

test("startKeeping_SameThemeRestoredInAnotherBrowserSince_IgnoresTheNotedChoice", () => {
  const restored: AccountTheme = { theme: "dark", choiceId: "8c3f2e4a-5d6b-8a7c-9d8e-0f1a2b3c4d5e" };

  assert.equal(startKeeping(meera, restored, noted("light", darkAccount.choiceId)).noted, undefined);
});

test("startKeeping_ChoiceNotedByAnotherPersonOnThisBrowser_IgnoresIt", () => {
  const keeping = startKeeping(meera, systemAccount, noted("dark", null, arjun));

  assert.equal(keeping.noted, undefined);
  assert.deepEqual(nextThemeStep(keeping, "dark")[1], { kind: "apply", theme: "system" });
});

test("nextThemeStep_PersonChoosesAnotherTheme_SavesItNotedOverTheAccountsChoice", () => {
  const [keeping, step] = nextThemeStep(watching(lightAccount), "dark");

  assert.deepEqual(step, { kind: "send", choice: noted("dark", lightAccount.choiceId) });
  assert.equal(keeping.sending, "dark");
});

test("nextThemeStep_SameThemeSeenAgain_DoesNothing", () => {
  const [chosen] = nextThemeStep(watching(systemAccount), "dark");

  assert.deepEqual(nextThemeStep(chosen, "dark")[1], { kind: "none" });
  assert.deepEqual(nextThemeStep(chosen, undefined)[1], { kind: "none" });
});

test("nextThemeStep_ChoiceWhileASaveIsOnItsWay_NotesItAndWaits", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");

  const [keeping, step] = nextThemeStep(sending, "light");

  assert.deepEqual(step, { kind: "note", choice: noted("light", null) });
  assert.equal(keeping.wanted, "light");
  assert.equal(keeping.sending, "dark");
});

test("themeAnswered_ChoiceBackToTheAccountsThemeWhileASaveWasOnItsWay_SavesItOverTheAnsweredChoice", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");
  const [waiting] = nextThemeStep(sending, "system");

  const [keeping, step] = themeAnswered(waiting, darkAccount);

  assert.deepEqual(step, { kind: "send", choice: noted("system", darkAccount.choiceId) });
  assert.equal(keeping.sending, "system");
  assert.equal(keeping.wanted, undefined);
});

test("themeAnswered_ChoiceBackToTheThemeOnItsWay_ForgetsTheNoteOnceItIsSaved", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");
  const [away] = nextThemeStep(sending, "light");
  const [back] = nextThemeStep(away, "dark");

  const [keeping, step] = themeAnswered(back, darkAccount);

  assert.equal(back.wanted, undefined);
  assert.deepEqual(step, { kind: "forget", choice: noted("dark", null) });
  assert.deepEqual(keeping.account, darkAccount);
  assert.equal(keeping.noted, undefined);
});

test("themeAnswered_SaveAnswered_ForgetsTheNoteAndKnowsTheAccountsNewChoice", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");

  const [keeping, step] = themeAnswered(sending, darkAccount);

  assert.deepEqual(step, { kind: "forget", choice: noted("dark", null) });
  assert.deepEqual(keeping.account, darkAccount);
  assert.equal(keeping.sending, undefined);
});

test("themeAnswered_SaveFailed_KeepsTheNoteForTheNextPage", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");

  const [keeping, step] = themeAnswered(sending, undefined);

  assert.deepEqual(step, { kind: "none" });
  assert.deepEqual(keeping.account, systemAccount);
  assert.deepEqual(keeping.noted, noted("dark", null));
  assert.equal(keeping.sending, undefined);
});

test("themeAnswered_SaveFailedWhileAnotherChoiceWaited_SavesTheWaitingChoice", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");
  const [waiting] = nextThemeStep(sending, "light");

  assert.deepEqual(themeAnswered(waiting, undefined)[1], { kind: "send", choice: noted("light", null) });
});

test("nextThemeStep_ChoiceBackToTheAccountsThemeAfterASaveFailed_ForgetsTheFailedChoice", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");
  const [failed] = themeAnswered(sending, undefined);

  const [keeping, step] = nextThemeStep(failed, "system");

  assert.deepEqual(step, { kind: "forget", choice: noted("dark", null) });
  assert.equal(keeping.noted, undefined);
});

test("nextThemeStep_ChoiceAfterASaveThatFailed_SavesTheNewChoice", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");
  const [failed] = themeAnswered(sending, undefined);

  assert.deepEqual(nextThemeStep(failed, "light")[1], { kind: "send", choice: noted("light", null) });
});

test("nextThemeStep_ChoiceBackToTheSavedThemeAfterAnAnswer_SavesNothing", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");
  const [saved] = themeAnswered(sending, darkAccount);
  const [chosen] = nextThemeStep(saved, "light");
  const [failed] = themeAnswered(chosen, undefined);

  assert.deepEqual(nextThemeStep(failed, "dark")[1], {
    kind: "forget",
    choice: noted("light", darkAccount.choiceId),
  });
});

test("accountRead_NoSaveOnItsWay_KnowsTheAccountAsRead", () => {
  assert.deepEqual(accountRead(watching(systemAccount), lightAccount).account, lightAccount);
});

test("accountRead_SaveOnItsWay_WaitsForItsAnswer", () => {
  const [sending] = nextThemeStep(watching(systemAccount), "dark");

  assert.deepEqual(accountRead(sending, lightAccount).account, systemAccount);
});

test("unsavedChoiceOf_ChoiceNotedWithinItsLifetime_IsThatChoice", () => {
  const first = noted("dark", null);
  const later = noted("light", darkAccount.choiceId);

  assert.deepEqual(unsavedChoiceOf(unsavedChoiceText(first, now), now), first);
  assert.deepEqual(unsavedChoiceOf(unsavedChoiceText(later, now - unsavedChoiceLifetimeMs), now), later);
});

test("unsavedChoiceOf_ChoiceNotedLongAgoOrLater_IsNone", () => {
  const choice = noted("dark", null);

  assert.equal(unsavedChoiceOf(unsavedChoiceText(choice, now - unsavedChoiceLifetimeMs - 1), now), undefined);
  assert.equal(unsavedChoiceOf(unsavedChoiceText(choice, now + 1), now), undefined);
});

test("unsavedChoiceOf_NothingOrSomethingElseStored_IsNone", () => {
  for (const stored of [
    null,
    "",
    "dark",
    "{",
    "null",
    "[]",
    `{"theme":"dark","over":null,"chosenAt":${now}}`,
    `{"person":"","theme":"dark","over":null,"chosenAt":${now}}`,
    `{"person":"${meera}","theme":"sepia","over":null,"chosenAt":${now}}`,
    `{"person":"${meera}","theme":"dark","chosenAt":${now}}`,
    `{"person":"${meera}","theme":"dark","over":"","chosenAt":${now}}`,
    `{"person":"${meera}","theme":"dark","over":3,"chosenAt":${now}}`,
    `{"person":"${meera}","theme":"dark","over":null}`,
  ]) {
    assert.equal(unsavedChoiceOf(stored, now), undefined, String(stored));
  }
});

test("isSameChoice_SamePersonThemeAndReplacedChoice_IsTheSameChoice", () => {
  const choice = noted("dark", null);

  assert.equal(isSameChoice(choice, { ...choice }), true);
  assert.equal(isSameChoice(choice, noted("dark", null, arjun)), false);
  assert.equal(isSameChoice(choice, noted("light", null)), false);
  assert.equal(isSameChoice(choice, noted("dark", darkAccount.choiceId)), false);
});

test("isThemePreference_EachThemeOfTheApi_IsATheme", () => {
  for (const theme of themePreferences) assert.equal(isThemePreference(theme), true);
  for (const value of [undefined, "", "Dark", "sepia", 1]) assert.equal(isThemePreference(value), false);
});

test("themePreferences_EveryThemeOfTheApi_InTheOrderTheChooserOffersThem", () => {
  assert.deepEqual(themePreferences, ["system", "light", "dark"]);
});
