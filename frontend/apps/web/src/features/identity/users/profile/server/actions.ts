"use server";

import { unstable_rethrow } from "next/navigation";
import { z } from "zod";

import { callApi } from "@/shared/api/client";
import { requireSignedInPerson } from "@/shared/auth/session";

import { isThemePreference, themePreferences, type AccountTheme } from "../components/theme-preference-state";

export type ThemeSaveOutcome =
  { readonly saved: true; readonly account: AccountTheme } | { readonly saved: false };

const chosenTheme = z.enum(themePreferences);

// The theme is the person's own setting, which the API saves for any signed-in person, so no permission is checked. A
// failure answers rather than throws, because the theme keeper saves in the background and tries again later.
export async function saveTheme(theme: unknown): Promise<ThemeSaveOutcome> {
  await requireSignedInPerson();
  const parsed = chosenTheme.safeParse(theme);
  if (!parsed.success) return { saved: false };

  try {
    const answer = await callApi((client) => client.api.identity.users.me.theme.put({ theme: parsed.data }));
    if (!isThemePreference(answer.theme)) return { saved: false };
    return { saved: true, account: { theme: answer.theme, choiceId: answer.choiceId ?? null } };
  } catch (error) {
    unstable_rethrow(error);
    return { saved: false };
  }
}
