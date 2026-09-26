import { test as base, expect, type Locator, type Page, type TestInfo } from "@playwright/test";
import { mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export type Theme = "light" | "dark";

export const themes: readonly Theme[] = ["light", "dark"];

type Fixtures = {
  theme: Theme;
  expectedConsoleError: RegExp | undefined;
  consoleErrors: string[];
  capture: (name: string, target?: Locator) => Promise<void>;
};

const screenshotsRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "screenshots");

const captureHiddenAttribute = "data-e2e-capture-hidden";

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
      page.on("pageerror", (error) => {
        if (!isExpected(error.message)) errors.push(error.message);
      });
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
    await use(async (name: string, target?: Locator) => {
      const file = screenshotPath(testInfo, name, theme);
      mkdirSync(dirname(file), { recursive: true });
      if (target === undefined) {
        await page.screenshot({ path: file, fullPage: true, animations: "disabled" });
      } else {
        await captureElement(target, file);
      }
      await testInfo.attach(`${name}--${theme}`, { path: file, contentType: "image/png" });
    });
  },
});

export type ThemedFixtures = { page: Page; capture: Fixtures["capture"]; theme: Theme };

export function forEachTheme(
  title: string,
  body: (fixtures: ThemedFixtures, isMobile: boolean) => Promise<void>,
): void {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });
      test(title, async ({ page, capture, isMobile }) => {
        await body({ page, capture, theme }, isMobile);
      });
    });
  }
}

// Radix radio groups move focus in a timeout after the arrow's keydown and check the newly focused radio only while an arrow key
// is still held, so the key is released once that radio is checked, as a person's key press is, never straight after keydown.
export async function pressArrowUntilChecked(page: Page, key: string, radio: Locator): Promise<void> {
  await page.keyboard.down(key);
  try {
    await expect(radio).toBeChecked();
  } finally {
    await page.keyboard.up(key);
  }
}

// An element taller than the viewport is captured from a scrolled page, where a sticky element outside it (the app header)
// would be painted across the middle of the image; hiding those for the capture keeps the image to the element alone.
async function captureElement(target: Locator, file: string): Promise<void> {
  await target.evaluate((element, attribute) => {
    for (const candidate of element.ownerDocument.body.querySelectorAll("*")) {
      if (candidate.contains(element) || element.contains(candidate)) continue;
      if (getComputedStyle(candidate).position === "sticky") candidate.setAttribute(attribute, "");
    }
  }, captureHiddenAttribute);
  try {
    await target.screenshot({
      path: file,
      animations: "disabled",
      style: `[${captureHiddenAttribute}] { visibility: hidden !important; }`,
    });
  } finally {
    await target.page().evaluate((attribute) => {
      for (const hidden of document.querySelectorAll(`[${attribute}]`)) hidden.removeAttribute(attribute);
    }, captureHiddenAttribute);
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
