import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { PeoplePage } from "../../../pages/identity/users/people.page";

test.describe("people list without the API", () => {
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
    "explains that people are unavailable instead of offering filters",
    async ({ page, capture }) => {
      const people = new PeoplePage(page);
      const renewal = sessionRenewed(page);
      await people.goto();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle(/People · ERP-AI-Pro/);
      await expect(people.listCard).toBeVisible();
      await expect(people.unavailable).toHaveRole("status");
      await expect(people.unavailable).toContainText("People are not available.");
      await expect(people.unavailable).toContainText("The API did not respond.");
      await expect(people.list.filters).toHaveCount(0);
      await expect(people.list.table).toHaveCount(0);
      await expect(people.list.cardList).toHaveCount(0);
      await expect(people.registerLink).toBeVisible();
      await expect(people.permissionsLink).toBeVisible();

      await capture("people-unavailable");
    },
  );
});
