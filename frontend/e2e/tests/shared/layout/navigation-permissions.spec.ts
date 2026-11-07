import {
  employeeNavigation,
  pageSearchMatches,
  pageSearchResultsSnapshot,
  pageSearchStatus,
  primaryNavigationSnapshot,
  registeredNavigation,
} from "../../../fixtures/navigation";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { StartPage } from "../../../pages/platform/home/start.page";
import { AppShell, showsBreadcrumbs } from "../../../pages/shared/layout/app-shell.page";

const systemInfoPath = "/platform/system-info";

test.describe("navigation for an employee", () => {
  forEachTheme(
    "lists only the pages the person may open, and a page they may not open names nothing they lack",
    async ({ page, capture }) => {
      const shell = new AppShell(page);
      const everyPage = pageSearchMatches("", employeeNavigation);
      await new StartPage(page).goto();
      await shell.waitUntilInteractive();

      await shell.openNavigation();
      await expect(shell.primaryNavigation).toMatchAriaSnapshot(
        primaryNavigationSnapshot(employeeNavigation),
      );
      for (const lacking of registeredNavigation.filter((entry) => !entry.employees)) {
        await expect(shell.navigationLink(lacking.title)).toHaveCount(0);
      }
      await capture("navigation-employee", shell.usesDrawer ? shell.drawerPanel : shell.sidebarPanel);
      await shell.closeDrawer();

      await shell.searchButton.click();
      await expect(shell.palette).toBeVisible();
      await expect(shell.paletteResults).toMatchAriaSnapshot(pageSearchResultsSnapshot(everyPage));
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(everyPage.length);
      await shell.paletteSearch.fill("recent starts");
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(
        pageSearchMatches("recent starts", employeeNavigation).length,
      );
      await expect(shell.paletteStatus).toHaveText(pageSearchStatus("recent starts", employeeNavigation));
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
