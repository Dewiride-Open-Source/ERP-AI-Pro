import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

test.describe("system information page without the API", () => {
  test.skip(
    !offlineBaseURL,
    "E2E_OFFLINE_BASE_URL is not set and Playwright did not start the offline web instance",
  );
  test.use({
    baseURL: offlineBaseURL ?? "",
    persona: null,
    expectedConsoleError: /the server responded with a status of 500/,
  });
  test.beforeEach(async ({ context }) => {
    await addUnverifiedSessionCookie(context, offlineBaseURL ?? "");
  });

  forEachTheme(
    "explains that the API is unreachable and keeps the refresh control",
    async ({ page, capture }) => {
      const systemInfo = new SystemInfoPage(page);
      const shell = new AppShell(page);
      const renewal = sessionRenewed(page);
      await systemInfo.goto();
      await expect(
        shell.userMenuButton,
        "the user menu of a session the API could not report",
      ).toHaveAccessibleName("Account");
      await shell.openUserMenu();
      await expect(shell.userMenu).toContainText("Your account");
      await expect(shell.signOutItem).toBeEnabled();
      await page.keyboard.press("Escape");
      await expect(shell.userMenu).toBeHidden();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(systemInfo.card).toBeVisible();
      await expect(systemInfo.unavailable).toBeVisible();
      await expect(systemInfo.unavailable).toHaveRole("status");
      await expect(systemInfo.unavailable).toContainText("The API is not reachable.");
      await expect(systemInfo.application).toHaveCount(0);
      await expect(systemInfo.refresh).toBeEnabled();

      await expect(systemInfo.startupsCard).toBeVisible();
      await expect(systemInfo.startupsUnavailable).toBeVisible();
      await expect(systemInfo.startupsUnavailable).toHaveRole("status");
      await expect(systemInfo.startupsUnavailable).toContainText("Recent starts are not available.");
      await expect(systemInfo.startupsTable).toHaveCount(0);
      await expect(systemInfo.startupsRows).toHaveCount(0);

      const refreshed = page.waitForResponse(
        (response) =>
          response.url().includes("/platform/system-info") && response.request().headers()["rsc"] === "1",
      );
      await systemInfo.refresh.click();
      expect((await refreshed).ok()).toBe(true);
      await expect(systemInfo.refresh).toBeEnabled();
      await expect(systemInfo.unavailable).toBeVisible();
      await expect(systemInfo.startupsUnavailable).toBeVisible();
      await expect(
        shell.userMenuButton,
        "the user menu after the page refreshed without its API",
      ).toHaveAccessibleName("Account");
      await expect(shell.userMenuButton).toBeEnabled();

      await capture("system-info-unavailable");
    },
  );
});
