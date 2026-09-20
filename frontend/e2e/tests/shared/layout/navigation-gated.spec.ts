import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

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
    async ({ page, capture }, isMobile) => {
      const shell = new AppShell(page);

      const response = await page.goto("/platform/system-info");
      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      if (!isMobile) {
        await expect(shell.primaryNavigation).toBeAttached();
        await expect(shell.navigationLink("System")).toHaveCount(0);
      }
      await expect(page.getByRole("main")).toMatchAriaSnapshot(`
        - main:
          - paragraph: "404"
          - heading "This page does not exist" [level=1]
          - paragraph: The link may be outdated or the page may have moved.
          - link "Go to sign in"
      `);
      await capture("module-disabled");

      await page.getByRole("link", { name: "Go to sign in" }).click();
      await expect(page).toHaveURL(/\/login$/);
    },
  );
});
