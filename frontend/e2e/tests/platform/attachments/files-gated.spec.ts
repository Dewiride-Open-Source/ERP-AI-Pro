import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

test.describe("attachments page with the attachments module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({
    baseURL: gatedBaseURL ?? "",
    expectedConsoleError: /the server responded with a status of 404/,
  });

  forEachTheme(
    "hides the module and answers not found for its page inside the shell",
    async ({ page, capture }, isMobile) => {
      const shell = new AppShell(page);

      const response = await page.goto("/platform/attachments");

      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      if (!isMobile) {
        await expect(shell.primaryNavigation).toBeAttached();
        await expect(shell.navigationLink("Attachments")).toHaveCount(0);
      }
      await capture("attachments-disabled");
    },
  );
});
