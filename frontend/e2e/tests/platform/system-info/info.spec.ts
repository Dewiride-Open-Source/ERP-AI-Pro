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
    const card = await systemInfo.card.boundingBox();
    for (const item of [systemInfo.application, systemInfo.version, systemInfo.started, systemInfo.uptime]) {
      const box = await item.boundingBox();
      expect(box!.x + box!.width).toBeLessThanOrEqual(card!.x + card!.width);
    }

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

    await expect(systemInfo.startupsCard).toBeVisible();
    await expect(systemInfo.startupsUnavailable).toHaveCount(0);
    await expect(systemInfo.startupsEmpty).toHaveCount(0);
    await expect(systemInfo.startupsRows.first()).toBeVisible();
    expect(await systemInfo.startupsRows.count()).toBeGreaterThanOrEqual(1);
    await expect(systemInfo.startupsVersion).toContainText(/\d+\.\d+\.\d+/);

    await expect(systemInfo.startupsTable).toMatchAriaSnapshot(`
      - table:
        - rowgroup:
          - row "Started Version Framework Environment Recorded":
            - columnheader "Started"
            - columnheader "Version"
            - columnheader "Framework"
            - columnheader "Environment"
            - columnheader "Recorded"
        - rowgroup:
          - row /\\d+\\.\\d+\\.\\d+/:
            - cell /\\d{4}/
            - cell /\\d+\\.\\d+\\.\\d+/
            - cell /\\.NET \\d+\\.\\d+/
            - cell /\\w+/
            - cell /\\d{4}/
    `);

    const refreshed = page.waitForResponse(
      (response) =>
        response.url().includes("/platform/system-info") && response.request().headers()["rsc"] === "1",
    );
    await systemInfo.refresh.click();
    expect((await refreshed).ok()).toBe(true);
    await expect(systemInfo.refresh).toBeEnabled();
    await expect(systemInfo.uptime).toContainText(/\d+s/);
    await expect(systemInfo.startupsRows.first()).toBeVisible();

    await capture("system-info");
  });
});
