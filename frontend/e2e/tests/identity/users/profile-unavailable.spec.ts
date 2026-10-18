import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { ownPhotoApi, ProfilePage } from "../../../pages/identity/users/profile.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

test.describe("My profile without the API", () => {
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
    "explains that the profile cannot be shown, without a photo to load or a theme to save",
    async ({ page, capture }) => {
      const profile = new ProfilePage(page);
      const shell = new AppShell(page);
      const photoRequests: string[] = [];
      page.on("request", (request) => {
        if (new URL(request.url()).pathname === ownPhotoApi) photoRequests.push(request.url());
      });
      const renewal = sessionRenewed(page);
      await profile.goto();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle(/My profile · ERP-AI-Pro/);
      await expect(profile.unavailable).toHaveRole("status");
      await expect(profile.unavailable).toContainText("Your profile cannot be shown right now.");
      await expect(profile.unavailable).toContainText(
        "The ERP could not read your details. Try again in a moment.",
      );
      await expect(profile.person).toHaveCount(0);
      await expect(profile.theme).toHaveCount(0);
      await expect(shell.userMenuButton).toHaveAccessibleName("Account");
      await expect(shell.userMenuButton.locator("[data-slot='avatar-image']")).toHaveCount(0);
      expect(photoRequests, "requests for the photo").toEqual([]);

      await capture("profile-unavailable");
    },
  );
});
