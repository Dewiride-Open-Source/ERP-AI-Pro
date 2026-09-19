import { test as base, expect, type Page, type TestInfo } from "@playwright/test";
import { mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export type Theme = "light" | "dark";

export const themes: readonly Theme[] = ["light", "dark"];

type Fixtures = {
  theme: Theme;
  expectedConsoleError: RegExp | undefined;
  consoleErrors: string[];
  capture: (name: string) => Promise<void>;
};

const screenshotsRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "screenshots");

export const test = base.extend<Fixtures>({
  theme: ["light", { option: true }],
  expectedConsoleError: [undefined, { option: true }],

  consoleErrors: [
    async ({ page, expectedConsoleError }, use) => {
      const errors: string[] = [];
      const isExpected = (text: string) => expectedConsoleError?.test(text) ?? false;
      page.on("console", (message) => {
        if (message.type() === "error" && !isExpected(message.text())) errors.push(message.text());
      });
      page.on("pageerror", (error) => errors.push(error.message));
      await use(errors);
      expect(errors, "console errors").toEqual([]);
    },
    { auto: true },
  ],

  page: async ({ page, theme }, use) => {
    await page.emulateMedia({ colorScheme: theme });
    await use(page);
  },

  capture: async ({ page, theme }, use, testInfo) => {
    await use(async (name: string) => {
      const file = screenshotPath(testInfo, name, theme);
      mkdirSync(dirname(file), { recursive: true });
      await page.screenshot({ path: file, fullPage: true });
      await testInfo.attach(`${name}--${theme}`, { path: file, contentType: "image/png" });
    });
  },
});

export type ThemedFixtures = { page: Page; capture: Fixtures["capture"]; theme: Theme };

export function forEachTheme(title: string, body: (fixtures: ThemedFixtures) => Promise<void>): void {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });
      test(title, async ({ page, capture }) => {
        await body({ page, capture, theme });
      });
    });
  }
}

function screenshotPath(testInfo: TestInfo, name: string, theme: Theme): string {
  const spec = (testInfo.titlePath[0] ?? "unknown").replaceAll("\\", "/").replace(/\.spec\.ts$/, "");
  return join(screenshotsRoot, spec, `${slug(name)}--${testInfo.project.name}--${theme}.png`);
}

function slug(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+/, "")
    .replace(/-+$/, "");
}

export { expect };
