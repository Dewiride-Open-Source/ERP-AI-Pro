import { addUnverifiedSessionCookie } from "../../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../../fixtures/test";
import { sessionRenewed } from "../../../../pages/identity/auth/session.page";
import { DepartmentsPage } from "../../../../pages/identity/users/departments.page";
import { DesignationsPage } from "../../../../pages/identity/users/designations.page";
import { OrgChartPage } from "../../../../pages/identity/users/org-chart.page";

test.describe("organisation pages without the API", () => {
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

  forEachTheme("explains that the departments are not available", async ({ page, capture }) => {
    const departments = new DepartmentsPage(page);
    const renewal = sessionRenewed(page);
    await departments.goto();
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle(/Departments · ERP-AI-Pro/);
    await expect(departments.card).toBeVisible();
    await expect(departments.unavailable).toHaveRole("status");
    await expect(departments.unavailable).toContainText("The departments are not available.");
    await expect(departments.unavailable).toContainText("The API did not respond.");
    await expect(departments.addButton).toHaveCount(0);

    await capture("departments-unavailable");
  });

  forEachTheme("explains that the designations are not available", async ({ page, capture }) => {
    const designations = new DesignationsPage(page);
    const renewal = sessionRenewed(page);
    await designations.goto();
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle(/Designations · ERP-AI-Pro/);
    await expect(designations.card).toBeVisible();
    await expect(designations.unavailable).toHaveRole("status");
    await expect(designations.unavailable).toContainText("The designations are not available.");
    await expect(designations.unavailable).toContainText("The API did not respond.");
    await expect(designations.addButton).toHaveCount(0);

    await capture("designations-unavailable");
  });

  forEachTheme("explains that the organisation chart is not available", async ({ page, capture }) => {
    const chart = new OrgChartPage(page);
    const renewal = sessionRenewed(page);
    await chart.goto();
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle(/Organisation chart · ERP-AI-Pro/);
    await expect(chart.chartHeading).toHaveText("Today's chart");
    await expect(chart.unavailable).toHaveRole("status");
    await expect(chart.unavailable).toContainText("The organisation chart is not available.");
    await expect(chart.unavailable).toContainText("The API did not respond.");
    await expect(chart.filters).toHaveCount(0);

    await capture("org-chart-unavailable");
  });
});
