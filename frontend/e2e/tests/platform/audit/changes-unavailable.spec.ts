import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { AuditTrailPage } from "../../../pages/platform/audit/changes.page";

test.describe("audit trail without the API", () => {
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
    "explains that the audit trail is unavailable instead of offering filters or a download",
    async ({ page, capture }) => {
      const auditTrail = new AuditTrailPage(page);
      const renewal = sessionRenewed(page);
      await auditTrail.goto();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle(/Audit trail · ERP-AI-Pro/);
      await expect(auditTrail.card).toBeVisible();
      await expect(auditTrail.unavailable).toHaveRole("status");
      await expect(auditTrail.unavailable).toContainText("The audit trail is not available.");
      await expect(auditTrail.unavailable).toContainText("The API did not respond.");
      await expect(auditTrail.timeline).toHaveCount(0);
      await expect(auditTrail.list.filters).toHaveCount(0);
      await expect(auditTrail.exportButton).toHaveCount(0);

      await capture("audit-trail-unavailable");
    },
  );
});
