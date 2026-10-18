import type { ThemePreference } from "@dewiride/erp-api-client";

export const themePreferences = ["system", "light", "dark"] as const satisfies readonly ThemePreference[];

export const unsavedChoiceKey = "erp-theme-unsaved";

export const unsavedChoiceLifetimeMs = 2 * 60 * 1000;

// The theme the person's account holds and the id of that choice, which changes whenever the theme does and is null until
// the person first chooses a theme other than system.
export type AccountTheme = { readonly theme: ThemePreference; readonly choiceId: string | null };

// A choice noted in this browser until its save answers: whose it is, the theme, and the account's choice it replaced.
export type UnsavedChoice = {
  readonly person: string;
  readonly theme: ThemePreference;
  readonly over: string | null;
};

export type ThemeKeeping = {
  readonly person: string;
  readonly account: AccountTheme;
  readonly previous: ThemePreference | undefined;
  readonly noted: UnsavedChoice | undefined;
  readonly sending: ThemePreference | undefined;
  readonly wanted: ThemePreference | undefined;
};

export type ThemeStep =
  | { readonly kind: "apply"; readonly theme: ThemePreference }
  | { readonly kind: "send"; readonly choice: UnsavedChoice }
  | { readonly kind: "note"; readonly choice: UnsavedChoice }
  | { readonly kind: "forget"; readonly choice: UnsavedChoice }
  | { readonly kind: "none" };

const nothing: ThemeStep = { kind: "none" };

export function isThemePreference(value: unknown): value is ThemePreference {
  return typeof value === "string" && (themePreferences as readonly string[]).includes(value);
}

export function isSameChoice(one: UnsavedChoice, other: UnsavedChoice): boolean {
  return one.person === other.person && one.theme === other.theme && one.over === other.over;
}

// A choice noted in this browser counts only for the person who made it, and only while the account still holds the choice
// it replaced: a page loaded while its save was still on the way read that choice, while any theme saved since, here or in
// another browser, is a later choice.
export function startKeeping(
  person: string,
  account: AccountTheme,
  found: UnsavedChoice | undefined,
): ThemeKeeping {
  const counts = found?.person === person && found.over === account.choiceId;
  return {
    person,
    account,
    previous: undefined,
    noted: counts ? found : undefined,
    sending: undefined,
    wanted: undefined,
  };
}

// Until the browser shows the theme the account holds, the keeper puts that theme in place, so nothing the browser held
// before is saved, unless the browser shows a choice noted here that counts: that choice is the person's, so it is saved
// instead. From then on every change is the person's own choice. One save is on its way at a time: a choice made meanwhile
// waits for its answer and is then saved unless the account holds it by then. A theme seen again without a change, as when
// React runs an effect twice, does nothing.
export function nextThemeStep(
  keeping: ThemeKeeping,
  shown: ThemePreference | undefined,
): [ThemeKeeping, ThemeStep] {
  if (keeping.previous === undefined) {
    const { noted, account } = keeping;
    if (noted !== undefined && shown === noted.theme) {
      const watching = { ...keeping, previous: shown };
      return shown === account.theme ? forget(watching) : send(watching, shown);
    }

    if (shown !== account.theme) return [keeping, { kind: "apply", theme: account.theme }];
    return forget({ ...keeping, previous: shown });
  }

  if (shown === undefined || shown === keeping.previous) return [keeping, nothing];
  const moved = { ...keeping, previous: shown };
  if (moved.sending !== undefined) {
    const choice = choiceOf(moved, shown);
    return [
      { ...moved, noted: choice, wanted: shown === moved.sending ? undefined : shown },
      { kind: "note", choice },
    ];
  }

  return shown === moved.account.theme ? forget(moved) : send(moved, shown);
}

// A save that fails changes nothing on screen, because the theme still applies here, and its note stays, so the next page
// this browser loads saves it again while the account still holds the choice it replaced.
export function themeAnswered(
  keeping: ThemeKeeping,
  answer: AccountTheme | undefined,
): [ThemeKeeping, ThemeStep] {
  const settled = { ...keeping, account: answer ?? keeping.account, sending: undefined, wanted: undefined };
  const { wanted } = keeping;
  if (wanted !== undefined && wanted !== settled.account.theme) return send(settled, wanted);
  if (wanted === undefined && answer === undefined) return [settled, nothing];
  return forget(settled);
}

// A later read of the account, as a refresh of the page brings, replaces what the keeper knows unless a save is on its way,
// whose answer is the newer.
export function accountRead(keeping: ThemeKeeping, account: AccountTheme): ThemeKeeping {
  return keeping.sending === undefined ? { ...keeping, account } : keeping;
}

// A choice whose save never answered counts for a short while only, so a choice left unsaved long ago never overrides one
// the person has saved since.
export function unsavedChoiceOf(stored: string | null, now: number): UnsavedChoice | undefined {
  if (stored === null) return undefined;
  let parsed: unknown;
  try {
    parsed = JSON.parse(stored);
  } catch {
    return undefined;
  }

  if (typeof parsed !== "object" || parsed === null) return undefined;
  const { person, theme, over, chosenAt } = parsed as Record<string, unknown>;
  const fresh = typeof chosenAt === "number" && chosenAt <= now && now - chosenAt <= unsavedChoiceLifetimeMs;
  const replaced = over === null || (typeof over === "string" && over !== "");
  return fresh && typeof person === "string" && person !== "" && isThemePreference(theme) && replaced
    ? { person, theme, over }
    : undefined;
}

export function unsavedChoiceText(choice: UnsavedChoice, now: number): string {
  return JSON.stringify({ person: choice.person, theme: choice.theme, over: choice.over, chosenAt: now });
}

function choiceOf(keeping: ThemeKeeping, theme: ThemePreference): UnsavedChoice {
  return { person: keeping.person, theme, over: keeping.account.choiceId };
}

function send(keeping: ThemeKeeping, theme: ThemePreference): [ThemeKeeping, ThemeStep] {
  const choice = choiceOf(keeping, theme);
  return [
    { ...keeping, noted: choice, sending: theme },
    { kind: "send", choice },
  ];
}

function forget(keeping: ThemeKeeping): [ThemeKeeping, ThemeStep] {
  return keeping.noted === undefined
    ? [keeping, nothing]
    : [
        { ...keeping, noted: undefined },
        { kind: "forget", choice: keeping.noted },
      ];
}
