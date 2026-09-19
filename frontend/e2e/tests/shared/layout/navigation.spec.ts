import { expect, forEachTheme, test } from "../../../fixtures/test";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

test.describe("app shell navigation", () => {
  test("primary navigation and the wordmark reach their targets", async ({ page, isMobile }) => {
    test.skip(isMobile, "the primary navigation is hidden on mobile until the drawer arrives");
    const shell = new AppShell(page);
    await new SystemInfoPage(page).goto();

    await shell.primaryNavigation.getByRole("link", { name: "System" }).click();
    await expect(page).toHaveURL(/\/platform\/system-info$/);
    await shell.wordmarkLink.click();
    await expect(page).toHaveURL(/\/login$/);
  });

  test.describe("not found", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("unknown routes render the not-found page", async ({ page, capture }) => {
      const response = await page.goto("/does-not-exist");
      expect(response?.status()).toBe(404);
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await capture("not-found");
      await page.getByRole("link", { name: "Go to sign in" }).click();
      await expect(page).toHaveURL(/\/login$/);
    });
  });
});
