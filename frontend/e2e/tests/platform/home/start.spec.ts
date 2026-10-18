import { expect, forEachTheme, test } from "../../../fixtures/test";
import { departmentsPath } from "../../../pages/identity/users/departments.page";
import { designationsPath } from "../../../pages/identity/users/designations.page";
import { orgChartPath } from "../../../pages/identity/users/org-chart.page";
import { peoplePath } from "../../../pages/identity/users/people.page";
import { attachmentsPath } from "../../../pages/platform/attachments/files.page";
import { auditTrailPath } from "../../../pages/platform/audit/changes.page";
import { StartPage } from "../../../pages/platform/home/start.page";

const organisationPages = [
  ["Organisation chart", orgChartPath],
  ["Departments", departmentsPath],
  ["Designations", designationsPath],
] as const;

test.describe("start page", () => {
  test.use({ persona: "administrator" });

  forEachTheme("greets the person and opens every area from its card", async ({ page, capture }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(page).toHaveTitle(/Home · ERP-AI-Pro/);
    await expect(start.areas).toHaveCount(7);
    await expect(start.noAreas).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - paragraph: Home
      - heading /^Welcome, / [level=1]
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
            - text: /Upload and download the files/
            - link "Open Attachments"
          - listitem:
            - heading "Audit trail" [level=3]
            - text: /who changed what in the ERP/
            - link "Open Audit trail"
          - listitem:
            - heading "People" [level=3]
            - text: /Register people, assign their roles/
            - link "Open People"
          - listitem:
            - heading "Organisation chart" [level=3]
            - text: /who reports to whom/
            - link "Open Organisation chart"
          - listitem:
            - heading "Departments" [level=3]
            - text: /which department each one sits under/
            - link "Open Departments"
          - listitem:
            - heading "Designations" [level=3]
            - text: /designations people can hold/
            - link "Open Designations"
    `);
    await capture("start");

    await start.openLink("Attachments").click();
    await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
    await page.goBack();
    await expect(start.heading).toBeVisible();
    await start.openLink("Audit trail").click();
    await expect(page).toHaveURL((url) => url.pathname === auditTrailPath);
    await page.goBack();
    await expect(start.heading).toBeVisible();
    await start.openLink("People").click();
    await expect(page).toHaveURL((url) => url.pathname === peoplePath);
    await page.goBack();
    await expect(start.heading).toBeVisible();
    for (const [title, path] of organisationPages) {
      await start.openLink(title).click();
      await expect(page).toHaveURL((url) => url.pathname === path);
      await page.goBack();
      await expect(start.heading).toBeVisible();
    }
    await start.openLink("System").click();
    await expect(page).toHaveURL((url) => url.pathname === "/platform/system-info");
  });

  test("names the signed-in person", async ({ page, person }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(start.heading).toHaveText(`Welcome, ${person?.name ?? ""}`);
  });
});

test.describe("start page for an employee", () => {
  forEachTheme("shows only the areas the person may open", async ({ page, capture }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(start.areas).toHaveCount(4);
    await expect(start.noAreas).toHaveCount(0);
    await expect(start.openLink("System")).toHaveCount(0);
    await expect(start.openLink("People")).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - paragraph: Home
      - heading /^Welcome, / [level=1]
      - paragraph: Choose where to start.
      - region "Areas you can open":
        - heading "Areas you can open" [level=2]
        - list:
          - listitem:
            - heading "Attachments" [level=3]
            - text: /Upload and download the files/
            - link "Open Attachments"
          - listitem:
            - heading "Organisation chart" [level=3]
            - text: /who reports to whom/
            - link "Open Organisation chart"
          - listitem:
            - heading "Departments" [level=3]
            - text: /which department each one sits under/
            - link "Open Departments"
          - listitem:
            - heading "Designations" [level=3]
            - text: /designations people can hold/
            - link "Open Designations"
    `);
    await capture("start-employee");

    for (const [title, path] of organisationPages) {
      await start.openLink(title).click();
      await expect(page).toHaveURL((url) => url.pathname === path);
      await page.goBack();
      await expect(start.heading).toBeVisible();
    }
    await start.openLink("Attachments").click();
    await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
  });
});
