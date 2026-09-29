export type ResolvedTheme = "light" | "dark";

export const themeStorageKey = "theme";

export function resolveStoredTheme(stored: string | null, prefersDark: boolean): ResolvedTheme {
  if (stored === "light" || stored === "dark") return stored;
  return prefersDark ? "dark" : "light";
}
