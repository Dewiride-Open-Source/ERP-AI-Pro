"use client";

import { useTheme } from "next-themes";

import { isThemeChoice, type ThemeChoice } from "./theme-choice";

export type { ThemeChoice } from "./theme-choice";

export type ThemeChoiceState = {
  readonly choice: ThemeChoice | undefined;
  readonly choose: (choice: ThemeChoice) => void;
};

// The theme chosen in this browser, as ThemeToggle shows and changes it; a value the browser's storage holds that names no
// theme counts as none.
export function useThemeChoice(): ThemeChoiceState {
  const { theme, setTheme } = useTheme();

  return { choice: isThemeChoice(theme) ? theme : undefined, choose: setTheme };
}
