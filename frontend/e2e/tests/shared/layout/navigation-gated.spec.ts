import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { AppShell, showsBreadcrumbs } from "../../../pages/shared/layout/app-shell.page";

test.describe("app shell navigation with the system-info module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({
    baseURL: gatedBaseURL ?? "",
    expectedConsoleError: /the server responded with a status of 404/,
  });

  forEachTheme(
    "hides the disabled module and answers not found for its pages inside the shell",
    async ({ page, capture }) => {
      const shell = new AppShell(page);

      const response = await page.goto("/platform/system-info");
      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("System")).toHaveCount(0);
      await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
      await shell.closeDrawer();
      await expect(page.getByRole("main")).toMatchAriaSnapshot(`
        - main:
          - paragraph: "404"
          - heading "This page does not exist" [level=1]
          - paragraph: The link may be outdated or the page may have moved.
          - link "Go to the home page"
      `);
      await capture("module-disabled");

      await page.getByRole("link", { name: "Go to the home page" }).click();
      await expect(page).toHaveURL((url) => url.pathname === "/");
    },
  );

  test("the page search offers only Home while every module is disabled", async ({ page }) => {
    const shell = new AppShell(page);
    const response = await page.goto("/platform/system-info");
    expect(response?.status()).toBe(404);
    await shell.waitUntilInteractive();

    await shell.searchButton.click();
    await expect(shell.palette).toBeVisible();
    await expect(shell.paletteResults.getByRole("option")).toHaveCount(1);
    await expect(shell.paletteOption("Home")).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(shell.palette).toBeHidden();
  });

  test("the breadcrumb of a disabled module's page is Home alone", async ({ page }) => {
    test.skip(!showsBreadcrumbs(page.viewportSize()), "the breadcrumb shows from 1024 px wide");
    const shell = new AppShell(page);
    const response = await page.goto("/platform/system-info");
    expect(response?.status()).toBe(404);
    await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();

    await expect(shell.breadcrumbs).toMatchAriaSnapshot(`
      - navigation "Breadcrumb":
        - list:
          - listitem:
            - link "Home":
              - /url: /
    `);
    await expect(shell.breadcrumbs.getByRole("listitem")).toHaveCount(1);
    await expect(shell.breadcrumbs.getByRole("link", { name: "Home" })).not.toHaveAttribute("aria-current");
  });
});
