"use client";

import type { ThemePreference as SavedTheme } from "@dewiride/erp-api-client";
import { useThemeChoice } from "@dewiride/erp-ui/components/theme/use-theme-choice";
import { unstable_rethrow } from "next/navigation";
import { useEffect, useRef, useTransition } from "react";

import { saveTheme } from "../server/actions";

import {
  nextThemeStep,
  startKeeping,
  themeSaved,
  unsavedChoiceKey,
  unsavedChoiceOf,
  unsavedChoiceText,
  type ThemeKeeping,
} from "./theme-preference-state";

// Storage throws when the browser blocks this site's data; a choice is then saved only while its page stays open.
function inStorage<T>(access: (storage: Storage) => T, blocked: T): T {
  try {
    return access(window.localStorage);
  } catch {
    return blocked;
  }
}

function takeUnsavedChoice(): SavedTheme | undefined {
  return inStorage((storage) => {
    const stored = storage.getItem(unsavedChoiceKey);
    storage.removeItem(unsavedChoiceKey);
    return unsavedChoiceOf(stored, Date.now());
  }, undefined);
}

function noteUnsavedChoice(theme: SavedTheme): void {
  inStorage((storage) => storage.setItem(unsavedChoiceKey, unsavedChoiceText(theme, Date.now())), undefined);
}

function forgetUnsavedChoice(theme: SavedTheme): void {
  inStorage((storage) => {
    if (unsavedChoiceOf(storage.getItem(unsavedChoiceKey), Date.now()) === theme)
      storage.removeItem(unsavedChoiceKey);
  }, undefined);
}

// Mounted once by the shell: it puts the theme the person saved in place when this browser holds another, and saves every
// theme they choose afterwards, with the top bar's toggle or on My profile, so their choice follows them to every browser. A
// save that fails changes nothing on screen, because the theme still applies here, and the next choice saves again; a choice
// whose save had not answered when its page was left is saved by the next page this browser loads.
export function ThemePreference({ saved }: { saved: SavedTheme }) {
  const { choice, choose } = useThemeChoice();
  const keeping = useRef<ThemeKeeping | undefined>(undefined);
  const [, startSaving] = useTransition();

  useEffect(() => {
    if (keeping.current !== undefined) keeping.current = themeSaved(keeping.current, saved);
  }, [saved]);

  useEffect(() => {
    const [next, step] = nextThemeStep(keeping.current ?? startKeeping(saved, takeUnsavedChoice()), choice);
    keeping.current = next;
    if (step.kind === "apply") choose(step.theme);
    if (step.kind !== "save") return;

    const chosen = step.theme;
    noteUnsavedChoice(chosen);
    startSaving(async () => {
      try {
        const outcome = await saveTheme(chosen);
        if (!outcome.saved) return;
        if (keeping.current !== undefined) keeping.current = themeSaved(keeping.current, chosen);
        forgetUnsavedChoice(chosen);
      } catch (error) {
        unstable_rethrow(error);
      }
    });
  }, [choice, choose, saved]);

  return null;
}
