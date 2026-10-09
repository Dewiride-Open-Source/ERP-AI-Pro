import { expect, forEachTheme, test } from "../../../fixtures/test";
import { StartPage } from "../../../pages/platform/home/start.page";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

test.describe("app shell navigation", () => {
  test("primary navigation and the wordmark reach their targets", async ({ page, isMobile }) => {
    test.skip(isMobile, "the primary navigation is hidden on mobile until the drawer arrives");
    const shell = new AppShell(page);
    await new SystemInfoPage(page).goto();

    await expect(shell.navigationLink("System")).toBeVisible();
    await expect(shell.primaryNavigation).toMatchAriaSnapshot(`
      - navigation "Primary":
        - link "System"
        - link "Attachments"
    `);
    await shell.navigationLink("System").click();
    await expect(page).toHaveURL(/\/platform\/system-info$/);
    await shell.wordmarkLink.click();
    await expect(page).toHaveURL((url) => url.pathname === "/");
    await expect(new StartPage(page).heading).toBeVisible();
  });

  forEachTheme("shows who is signed in beside the sign-out control", async ({ page, capture }) => {
    const shell = new AppShell(page);
    await new StartPage(page).goto();

    await expect(shell.account).toMatchAriaSnapshot(`
      - paragraph: /^Signed in as /
      - button "Sign out"
    `);
    await expect(shell.signOut).toBeEnabled();
    if ((page.viewportSize()?.width ?? 0) >= 768) {
      await expect(shell.account.getByRole("paragraph")).toBeVisible();
    }
    await capture("header-signed-in", shell.banner);
  });

  test.describe("not found", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("unknown routes render the not-found page", async ({ page, capture }) => {
      const response = await page.goto("/does-not-exist");
      expect(response?.status()).toBe(404);
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await capture("not-found");
      await page.getByRole("link", { name: "Go to the home page" }).click();
      await expect(page).toHaveURL((url) => url.pathname === "/");
    });
  });
});
