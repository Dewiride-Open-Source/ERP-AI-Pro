import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { AttachmentsPage } from "../../../pages/platform/attachments/files.page";

test.describe("attachments page without the API", () => {
  test.skip(
    !offlineBaseURL,
    "E2E_OFFLINE_BASE_URL is not set and Playwright did not start the offline web instance",
  );
  test.use({ baseURL: offlineBaseURL ?? "" });

  forEachTheme(
    "explains that attachments are unavailable instead of offering an upload",
    async ({ page, capture }) => {
      const attachments = new AttachmentsPage(page);
      await attachments.goto();

      await expect(attachments.uploadCard).toBeVisible();
      await expect(attachments.listCard).toBeVisible();
      await expect(attachments.unavailable).toHaveCount(2);
      await expect(attachments.unavailable.first()).toHaveRole("status");
      await expect(attachments.unavailable.first()).toContainText("Attachments are not available.");
      await expect(attachments.dropZone).toHaveCount(0);
      await expect(attachments.table).toHaveCount(0);

      await capture("attachments-unavailable");
    },
  );
});
