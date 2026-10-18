export const themeChoices = ["system", "light", "dark"] as const;

export type ThemeChoice = (typeof themeChoices)[number];

export function isThemeChoice(value: unknown): value is ThemeChoice {
  return typeof value === "string" && (themeChoices as readonly string[]).includes(value);
}
