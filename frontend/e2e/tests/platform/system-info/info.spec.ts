import { expect, forEachTheme, test } from "../../../fixtures/test";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";

test.describe("system information page", () => {
  test.use({ persona: "administrator" });

  forEachTheme("shows the API identity and refreshes it", async ({ page, capture }, isMobile) => {
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
    const versionItem = await systemInfo.version.boundingBox();
    const versionBadge = await systemInfo.versionBadge.boundingBox();
    expect(versionBadge!.x + versionBadge!.width).toBeLessThanOrEqual(versionItem!.x + versionItem!.width);

    await expect(systemInfo.card).toMatchAriaSnapshot(`
      - term: Application
      - definition: ERP-AI-Pro
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
    const scroller = page.locator("[data-slot='table-container']").filter({ has: systemInfo.startupsTable });
    if (isMobile) {
      await expect(scroller).toHaveAttribute("tabindex", "0");
      await expect(page.getByRole("region", { name: "Recent starts" })).toBeVisible();
    } else {
      await expect(scroller).not.toHaveAttribute("tabindex");
      await expect(scroller).not.toHaveAttribute("role");
    }

    await systemInfo.refreshPage();
    await expect(systemInfo.uptime).toContainText(/\d+s/);
    await expect(systemInfo.startupsRows.first()).toBeVisible();

    await capture("system-info");
  });
});

test.describe("system information page for an employee", () => {
  forEachTheme("tells them they have no access to it", async ({ page, capture }) => {
    await page.goto("/platform/system-info");

    const noAccess = page.getByRole("heading", { name: "You do not have access to this page", level: 1 });
    await expect(noAccess).toBeVisible();
    await expect(page).toHaveTitle(/No access · ERP-AI-Pro/);
    await expect(page.getByTestId("system-info-card")).toHaveCount(0);
    await expect(page.getByTestId("recent-startups-card")).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - main:
        - heading "You do not have access to this page" [level=1]
        - paragraph: Ask an administrator for access.
        - link "Go to the home page"
    `);
    await capture("system-info-no-access");

    await page.getByRole("link", { name: "Go to the home page" }).click();
    await expect(page).toHaveURL((url) => url.pathname === "/");
  });

  test("the API refuses them the system information and the recent starts", async ({ api }) => {
    for (const path of ["/api/platform/system-info", "/api/platform/system-info/startups"]) {
      const refused = await api.get(path);
      expect(refused.status(), path).toBe(403);
      expect(await refused.json(), path).toMatchObject({ code: "request.forbidden" });
    }
  });
});
