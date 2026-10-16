import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { auditTrailPath, changesApi, exportApi } from "../../../pages/platform/audit/changes.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// An administrator holds the audit permissions, so only the disabled module can hide the page from them.
test.describe("audit trail with the audit module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "", persona: "administrator" });

  test.describe("its page", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme(
      "hides the module and answers not found for its page inside the shell",
      async ({ page, capture }) => {
        const shell = new AppShell(page);

        const response = await page.goto(auditTrailPath);

        expect(response?.status()).toBe(404);
        await expect(shell.banner).toBeVisible();
        await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
        await shell.openNavigation();
        await expect(shell.primaryNavigation).toBeVisible();
        await expect(shell.navigationLink("Audit trail")).toHaveCount(0);
        await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
        await shell.closeDrawer();
        await capture("audit-trail-disabled");
      },
    );
  });

  test("the API answers the changes and their download with the disabled module's problem", async ({
    api,
  }) => {
    for (const path of [changesApi, exportApi]) {
      const gated = await api.get(path);
      expect(gated.status(), path).toBe(404);
      expect(gated.headers()["content-type"], path).toContain("application/problem+json");
      expect(await gated.json(), path).toMatchObject({ code: "feature.disabled", instance: path });
    }
  });
});
