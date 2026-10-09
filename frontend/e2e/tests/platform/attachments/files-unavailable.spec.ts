import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { AttachmentsPage } from "../../../pages/platform/attachments/files.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

test.describe("attachments page without the API", () => {
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
    "explains that attachments are unavailable instead of offering an upload",
    async ({ page, capture }) => {
      const attachments = new AttachmentsPage(page);
      const shell = new AppShell(page);
      const renewal = sessionRenewed(page);
      await attachments.goto();
      await expect(shell.signOut, "the sign-out of a session the API could not report").toBeVisible();
      await expect(shell.account).not.toContainText("Signed in as");
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(attachments.uploadCard).toBeVisible();
      await expect(attachments.listCard).toBeVisible();
      await expect(attachments.unavailable).toHaveCount(2);
      await expect(attachments.unavailable.first()).toHaveRole("status");
      await expect(attachments.unavailable.first()).toContainText("Attachments are not available.");
      await expect(attachments.dropZone).toHaveCount(0);
      await expect(attachments.list.table).toHaveCount(0);
      await expect(attachments.list.cardList).toHaveCount(0);

      await capture("attachments-unavailable");
    },
  );
});
