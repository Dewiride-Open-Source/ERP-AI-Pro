import { registeredNavigationEntries } from "../../../fixtures/navigation";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { formKitPath } from "../../../pages/platform/design/form-kit.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

test.describe("navigation crawl", () => {
  forEachTheme(
    "follows every entry of the primary navigation to its page without a console error",
    async ({ page, capture, consoleErrors }) => {
      const shell = new AppShell(page);
      await page.goto(formKitPath);
      await shell.waitUntilInteractive();
      const entries = await registeredNavigationEntries(page);
      expect(entries.length, "navigation entries").toBeGreaterThan(0);
      // Each entry is a full page captured and scanned, which Firefox takes the longest over, so the budget grows with the
      // registry instead of every new module shrinking the time left for the others.
      test.setTimeout(15_000 + entries.length * 45_000);
      for (const entry of entries) {
        expect(entry.href, `address of ${entry.name}`).toMatch(/^\/[a-z0-9/-]+$/);
        const load = await page.request.get(entry.href, { maxRedirects: 0 });
        expect(load.status(), `status of a full load of ${entry.href}`).toBe(200);
      }

      for (const entry of entries) {
        await shell.openNavigation();
        await shell.navigationLink(entry.name).click();
        await expect(page).toHaveURL((url) => url.pathname === entry.href);
        await expect(shell.drawer).toBeHidden();
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
