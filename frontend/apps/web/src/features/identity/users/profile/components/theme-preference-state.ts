import type { ThemePreference } from "@dewiride/erp-api-client";

export const themePreferences = ["system", "light", "dark"] as const satisfies readonly ThemePreference[];

export const unsavedChoiceKey = "erp-theme-unsaved";

export const unsavedChoiceLifetimeMs = 2 * 60 * 1000;

export type ThemeKeeping = {
  readonly lastSaved: ThemePreference;
  readonly previous: ThemePreference | undefined;
  readonly unsaved: ThemePreference | undefined;
};

export type ThemeStep =
  | { readonly kind: "apply"; readonly theme: ThemePreference }
  | { readonly kind: "save"; readonly theme: ThemePreference }
  | { readonly kind: "none" };

const nothing: ThemeStep = { kind: "none" };

export function isThemePreference(value: unknown): value is ThemePreference {
  return typeof value === "string" && (themePreferences as readonly string[]).includes(value);
}

export function startKeeping(saved: ThemePreference, unsaved: ThemePreference | undefined): ThemeKeeping {
  return { lastSaved: saved, previous: undefined, unsaved };
}

// Until the browser shows the theme the account holds, the keeper puts that theme in place, so nothing the browser held
// before is saved, unless the browser shows a choice the person made here whose save never answered, as when they reloaded
// straight after choosing: that choice is theirs, so it is saved instead. From then on every change is the person's own
// choice, saved unless the account already holds it. A theme seen again without a change, as when React runs an effect
// twice, does nothing.
export function nextThemeStep(
  keeping: ThemeKeeping,
  shown: ThemePreference | undefined,
): [ThemeKeeping, ThemeStep] {
  if (keeping.previous === undefined) {
    if (keeping.unsaved !== undefined && shown === keeping.unsaved) {
      return [
        { ...keeping, previous: shown, unsaved: undefined },
        shown === keeping.lastSaved ? nothing : { kind: "save", theme: shown },
      ];
    }

    return shown === keeping.lastSaved
      ? [{ ...keeping, previous: shown, unsaved: undefined }, nothing]
      : [keeping, { kind: "apply", theme: keeping.lastSaved }];
  }

  if (shown === undefined || shown === keeping.previous) return [keeping, nothing];
  const moved = { ...keeping, previous: shown };
  return [moved, shown === keeping.lastSaved ? nothing : { kind: "save", theme: shown }];
}

export function themeSaved(keeping: ThemeKeeping, theme: ThemePreference): ThemeKeeping {
  return { ...keeping, lastSaved: theme };
}

// A choice whose save never answered counts for a short while only, so a choice left unsaved long ago never overrides one
// the person has saved since from another browser.
export function unsavedChoiceOf(stored: string | null, now: number): ThemePreference | undefined {
  if (stored === null) return undefined;
  let parsed: unknown;
  try {
    parsed = JSON.parse(stored);
  } catch {
    return undefined;
  }

  if (typeof parsed !== "object" || parsed === null) return undefined;
  const { theme, chosenAt } = parsed as Record<string, unknown>;
  const fresh = typeof chosenAt === "number" && chosenAt <= now && now - chosenAt <= unsavedChoiceLifetimeMs;
  return fresh && isThemePreference(theme) ? theme : undefined;
}

export function unsavedChoiceText(theme: ThemePreference, now: number): string {
  return JSON.stringify({ theme, chosenAt: now });
}
