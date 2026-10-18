import { expect, forEachTheme, test } from "../../../fixtures/test";
import { StartPage } from "../../../pages/platform/home/start.page";
import { AppShell, showsBreadcrumbs } from "../../../pages/shared/layout/app-shell.page";

const systemInfoPath = "/platform/system-info";

test.describe("navigation for an employee", () => {
  forEachTheme(
    "lists only the pages the person may open, and a page they may not open names nothing they lack",
    async ({ page, capture }) => {
      const shell = new AppShell(page);
      await new StartPage(page).goto();
      await shell.waitUntilInteractive();

      await shell.openNavigation();
      await expect(shell.primaryNavigation).toMatchAriaSnapshot(`
        - navigation "Primary":
          - list:
            - listitem:
              - link "Home":
                - /url: /
          - text: Platform
          - list "Platform":
            - listitem:
              - link "Attachments":
                - /url: /platform/attachments
          - text: Organisation
          - list "Organisation":
            - listitem:
              - link "Organisation chart":
                - /url: /identity/users/org-chart
            - listitem:
              - link "Departments":
                - /url: /identity/users/departments
            - listitem:
              - link "Designations":
                - /url: /identity/users/designations
      `);
      await expect(shell.navigationLink("System")).toHaveCount(0);
      await expect(shell.navigationLink("People")).toHaveCount(0);
      await capture("navigation-employee", shell.usesDrawer ? shell.drawerPanel : shell.sidebarPanel);
      await shell.closeDrawer();

      await shell.searchButton.click();
      await expect(shell.palette).toBeVisible();
      await expect(shell.paletteResults).toMatchAriaSnapshot(`
        - listbox "Pages":
          - option "Home" [selected]
          - group "Platform":
            - text: Platform
            - option "Attachments"
          - group "Organisation":
            - text: Organisation
            - option "Organisation chart"
            - option "Departments"
            - option "Designations"
      `);
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(5);
      await shell.paletteSearch.fill("recent starts");
      await expect(shell.paletteEmpty).toBeVisible();
      await expect(shell.paletteStatus).toHaveText("No page matches your search.");
      await page.keyboard.press("Escape");
      await expect(shell.palette).toBeHidden();

      await page.goto(systemInfoPath);
      await expect(
        page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
      ).toBeVisible();
      await shell.waitUntilInteractive();
      if (showsBreadcrumbs(page.viewportSize())) {
        await expect(shell.breadcrumbs.getByRole("listitem")).toHaveCount(1);
        await expect(shell.breadcrumbs.getByRole("link", { name: "Home" })).toBeVisible();
      }
      await shell.openNavigation();
      await expect(shell.navigationLink("System")).toHaveCount(0);
      await expect(shell.primaryNavigation.locator("[aria-current='page']")).toHaveCount(0);
      await shell.closeDrawer();
    },
  );
});
