import { registeredNavigationEntries } from "../../../fixtures/navigation";
import { expect, forEachTheme, test } from "../../../fixtures/test";

const startPath = "/design/form-kit";

test.describe("navigation crawl", () => {
  forEachTheme(
    "visits every entry of the primary navigation without a console error",
    async ({ page, capture, consoleErrors }) => {
      await page.goto(startPath);
      const entries = await registeredNavigationEntries(page);
      expect(entries.length, "navigation entries").toBeGreaterThan(0);
      // Each entry is a full page captured and scanned, which Firefox takes the longest over, so the budget grows with the
      // registry instead of every new module shrinking the time left for the others.
      test.setTimeout(15_000 + entries.length * 45_000);
      for (const entry of entries) {
        expect(entry.href, `address of ${entry.name}`).toMatch(/^\/[a-z0-9/-]+$/);
      }

      for (const entry of entries) {
        const response = await page.goto(entry.href);
        expect(response?.status(), `status of ${entry.href}`).toBe(200);
        await expect(page.getByRole("status", { name: /^Loading/ })).toHaveCount(0);
        await expect(
          page.getByRole("main").getByRole("heading", { level: 1 }),
          `heading of ${entry.href}`,
        ).toBeVisible();
        await expect(page.getByRole("heading", { name: "This page does not exist" })).toHaveCount(0);
        await capture(`crawl-${entry.name}`);
        expect(consoleErrors, `console errors on ${entry.href}`).toEqual([]);
      }
    },
  );
});
