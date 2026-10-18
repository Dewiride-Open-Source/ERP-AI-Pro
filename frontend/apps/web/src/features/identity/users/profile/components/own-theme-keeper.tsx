import { getOwnProfile } from "../server/queries";

import { ThemePreference } from "./theme-preference";
import { isThemePreference } from "./theme-preference-state";

// Without a record the API could read, the keeper has no saved theme to apply or replace, so this browser's theme stays its own.
export async function OwnThemeKeeper() {
  const read = await getOwnProfile();
  if (read.status !== "read") return null;
  const { id, theme, themeChoiceId } = read.profile;
  if (typeof id !== "string" || !isThemePreference(theme)) return null;
  return <ThemePreference person={id} account={{ theme, choiceId: themeChoiceId ?? null }} />;
}
