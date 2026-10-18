import { getOwnProfile } from "../server/queries";

import { ThemePreference } from "./theme-preference";

// Without a record the API could read, the keeper has no saved theme to apply or replace, so this browser's theme stays its own.
export async function OwnThemeKeeper() {
  const read = await getOwnProfile();
  const saved = read.status === "read" ? read.profile.theme : undefined;
  return saved === undefined || saved === null ? null : <ThemePreference saved={saved} />;
}
