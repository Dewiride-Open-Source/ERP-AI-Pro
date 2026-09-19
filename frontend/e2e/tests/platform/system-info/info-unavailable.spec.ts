import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";

test.describe("system information page without the API", () => {
  test.skip(
    !offlineBaseURL,
    "E2E_OFFLINE_BASE_URL is not set and Playwright did not start the offline web instance",
  );
  test.use({ baseURL: offlineBaseURL ?? "" });

  forEachTheme(
    "explains that the API is unreachable and keeps the refresh control",
    async ({ page, capture }) => {
      const systemInfo = new SystemInfoPage(page);
      await systemInfo.goto();

      await expect(systemInfo.card).toBeVisible();
      await expect(systemInfo.unavailable).toBeVisible();
      await expect(systemInfo.unavailable).toHaveRole("status");
      await expect(systemInfo.unavailable).toContainText("The API is not reachable.");
      await expect(systemInfo.application).toHaveCount(0);
      await expect(systemInfo.refresh).toBeEnabled();

      await systemInfo.refresh.click();
      await expect(systemInfo.refresh).toBeEnabled();
      await expect(systemInfo.unavailable).toBeVisible();

      await capture("system-info-unavailable");
    },
  );
});
