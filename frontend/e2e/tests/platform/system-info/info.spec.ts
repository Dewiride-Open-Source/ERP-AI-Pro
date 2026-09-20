import { expect, forEachTheme, test } from "../../../fixtures/test";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";

test.describe("system information page", () => {
  forEachTheme("shows the API identity and refreshes it", async ({ page, capture }) => {
    const systemInfo = new SystemInfoPage(page);
    await systemInfo.goto();

    await expect(page).toHaveTitle(/System information · ERP-AI-Pro/);
    await expect(systemInfo.card).toBeVisible();
    await expect(systemInfo.unavailable).toHaveCount(0);
    await expect(systemInfo.application).toContainText("ERP-AI-Pro");
    await expect(systemInfo.version).toContainText(/\d+\.\d+\.\d+/);
    await expect(systemInfo.started).toContainText(/\d{4}/);
    await expect(systemInfo.uptime).toContainText(/\d+s/);

    await expect(systemInfo.card).toMatchAriaSnapshot(`
      - term: Application
      - definition: /^ERP-AI-Pro( \\(local-dev\\))?$/
      - term: Version
      - definition: /\\d+\\.\\d+\\.\\d+/
      - term: Started
      - definition: /\\d{4}/
      - term: Uptime
      - definition: /\\d+s/
    `);

    const refreshed = page.waitForResponse(
      (response) =>
        response.url().includes("/platform/system-info") && response.request().headers()["rsc"] === "1",
    );
    await systemInfo.refresh.click();
    expect((await refreshed).ok()).toBe(true);
    await expect(systemInfo.refresh).toBeEnabled();
    await expect(systemInfo.uptime).toContainText(/\d+s/);

    await capture("system-info");
  });
});
