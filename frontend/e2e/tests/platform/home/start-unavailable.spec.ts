import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { StartPage } from "../../../pages/platform/home/start.page";

test.describe("start page without the API", () => {
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
    "welcomes without a name and still offers every area and Sign out",
    async ({ page, capture }) => {
      const start = new StartPage(page);
      const renewal = sessionRenewed(page);
      await start.goto();

      await expect(start.heading).toHaveText("Welcome");
      await expect(start.areas).toHaveCount(2);
      await expect(start.noAreas).toHaveCount(0);
      await expect(
        start.shell.userMenuButton,
        "the user menu of a session the API could not report",
      ).toHaveAccessibleName("Account");
      await start.shell.openUserMenu();
      await expect(start.shell.userMenu).toContainText("Your account");
      await expect(start.shell.signOutItem).toBeEnabled();
      await page.keyboard.press("Escape");
      await expect(start.shell.userMenu).toBeHidden();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);
      await expect(page.getByRole("main")).toMatchAriaSnapshot(`
        - paragraph: Home
        - heading "Welcome" [level=1]
        - paragraph: Choose where to start.
        - region "Areas you can open":
          - heading "Areas you can open" [level=2]
          - list:
            - listitem:
              - heading "System" [level=3]
              - text: /version of the ERP/
              - link "Open System"
            - listitem:
              - heading "Attachments" [level=3]
              - text: /Upload, download and delete/
              - link "Open Attachments"
      `);

      await capture("start-unavailable");
    },
  );
});
