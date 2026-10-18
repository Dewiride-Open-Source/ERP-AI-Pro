"use client";

import { useThemeChoice } from "@dewiride/erp-ui/components/theme/use-theme-choice";
import { unstable_rethrow } from "next/navigation";
import { useCallback, useEffect, useRef, useTransition } from "react";

import { saveTheme } from "../server/actions";

import {
  accountRead,
  isSameChoice,
  nextThemeStep,
  startKeeping,
  themeAnswered,
  unsavedChoiceKey,
  unsavedChoiceOf,
  unsavedChoiceText,
  type AccountTheme,
  type ThemeKeeping,
  type ThemeStep,
  type UnsavedChoice,
} from "./theme-preference-state";

// Storage throws when the browser blocks this site's data; a choice is then saved only while its page stays open.
function inStorage<T>(access: (storage: Storage) => T, blocked: T): T {
  try {
    return access(window.localStorage);
  } catch {
    return blocked;
  }
}

function storedChoice(storage: Storage): UnsavedChoice | undefined {
  return unsavedChoiceOf(storage.getItem(unsavedChoiceKey), Date.now());
}

// A stored choice that does not count for this page, another person's or one the account has moved past, is removed.
function startFromStorage(person: string, account: AccountTheme): ThemeKeeping {
  const keeping = startKeeping(person, account, inStorage(storedChoice, undefined));
  if (keeping.noted === undefined) inStorage((storage) => storage.removeItem(unsavedChoiceKey), undefined);
  return keeping;
}

function noteUnsavedChoice(choice: UnsavedChoice): void {
  inStorage((storage) => storage.setItem(unsavedChoiceKey, unsavedChoiceText(choice, Date.now())), undefined);
}

// Another tab of this browser may have noted a later choice since, which stays.
function forgetUnsavedChoice(choice: UnsavedChoice): void {
  inStorage((storage) => {
    const stored = storedChoice(storage);
    if (stored !== undefined && isSameChoice(stored, choice)) storage.removeItem(unsavedChoiceKey);
  }, undefined);
}

async function savedAccount(theme: UnsavedChoice["theme"]): Promise<AccountTheme | undefined> {
  try {
    const outcome = await saveTheme(theme);
    return outcome.saved ? outcome.account : undefined;
  } catch (error) {
    unstable_rethrow(error);
    return undefined;
  }
}

// Mounted once by the shell: it puts the theme the person saved in place when this browser holds another, and saves every
// theme they choose afterwards, with the top bar's toggle or on My profile, so their choice follows them to every browser.
// Each choice is noted in this browser until its save answers, so a page loaded before the save arrived, as when the person
// reloads straight after choosing, saves it again instead of putting the older theme back.
export function ThemePreference({ person, account }: { person: string; account: AccountTheme }) {
  const { choice, choose } = useThemeChoice();
  const keeping = useRef<ThemeKeeping | undefined>(undefined);
  const [, startSaving] = useTransition();
  const { theme: accountTheme, choiceId: accountChoiceId } = account;

  const perform = useCallback(
    function perform([next, step]: [ThemeKeeping, ThemeStep]) {
      keeping.current = next;
      switch (step.kind) {
        case "apply":
          choose(step.theme);
          return;
        case "note":
          noteUnsavedChoice(step.choice);
          return;
        case "forget":
          forgetUnsavedChoice(step.choice);
          return;
        case "send": {
          noteUnsavedChoice(step.choice);
          const { theme } = step.choice;
          startSaving(async () => {
            const answer = await savedAccount(theme);
            if (keeping.current !== undefined) perform(themeAnswered(keeping.current, answer));
          });
          return;
        }
        case "none":
          return;
      }
    },
    [choose],
  );

  useEffect(() => {
    if (keeping.current !== undefined) {
      keeping.current = accountRead(keeping.current, { theme: accountTheme, choiceId: accountChoiceId });
    }
  }, [accountTheme, accountChoiceId]);

  useEffect(() => {
    const current =
      keeping.current ?? startFromStorage(person, { theme: accountTheme, choiceId: accountChoiceId });
    perform(nextThemeStep(current, choice));
  }, [choice, person, accountTheme, accountChoiceId, perform]);

  return null;
}
