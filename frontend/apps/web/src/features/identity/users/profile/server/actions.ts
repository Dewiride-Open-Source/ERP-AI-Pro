"use server";

import { unstable_rethrow } from "next/navigation";
import { z } from "zod";

import { sendApi } from "@/shared/api/client";
import { requireSignedInPerson } from "@/shared/auth/session";

import { themePreferences } from "../components/theme-preference-state";

export type ThemeSaveOutcome = { readonly saved: boolean };

const chosenTheme = z.enum(themePreferences);

// The theme is the person's own setting, which the API saves for any signed-in person, so no permission is checked. A
// failure answers rather than throws, because the theme keeper saves in the background and tries again at the next choice.
export async function saveTheme(theme: unknown): Promise<ThemeSaveOutcome> {
  await requireSignedInPerson();
  const parsed = chosenTheme.safeParse(theme);
  if (!parsed.success) return { saved: false };

  try {
    await sendApi((client) => client.api.identity.users.me.theme.put({ theme: parsed.data }));
  } catch (error) {
    unstable_rethrow(error);
    return { saved: false };
  }
  return { saved: true };
}
