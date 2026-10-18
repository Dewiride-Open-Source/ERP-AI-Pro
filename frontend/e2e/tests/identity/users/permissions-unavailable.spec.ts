import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { PermissionsPage } from "../../../pages/identity/users/permissions.page";

test.describe("roles and permissions page without the API", () => {
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
    "explains that the roles and permissions are not available and keeps the way back to the people",
    async ({ page, capture }) => {
      const permissions = new PermissionsPage(page);
      const renewal = sessionRenewed(page);
      await permissions.goto();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle(/Roles and permissions · ERP-AI-Pro/);
      await expect(permissions.card).toBeVisible();
      await expect(permissions.unavailable).toHaveRole("status");
      await expect(permissions.unavailable).toContainText("The roles and permissions are not available.");
      await expect(permissions.unavailable).toContainText("The API did not respond.");
      await expect(permissions.matrix).toHaveCount(0);
      await expect(permissions.peopleLink).toBeVisible();

      await capture("permissions-unavailable");
    },
  );
});
