import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { centralTaxRatesKey } from "../../../pages/compliance/statutory-parameters/parameter-set.page";
import {
  parameterSetApi,
  parameterSetPath,
  parameterSetsApi,
  parameterSetsPath,
} from "../../../pages/compliance/statutory-parameters/parameter-sets.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// An administrator holds every permission of the module, so only the disabled module can hide its pages from them.
test.describe("statutory parameters with the statutory parameters module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "", persona: "administrator" });

  test.describe("their pages", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("hide the entry and answer not found inside the shell", async ({ page, capture }) => {
      const shell = new AppShell(page);

      const response = await page.goto(parameterSetsPath);

      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Statutory parameters")).toHaveCount(0);
      await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
      await shell.closeDrawer();
      await capture("parameter-sets-disabled");
    });

    test("answer not found for a set and every address of one", async ({ page }) => {
      for (const path of [
        parameterSetPath(centralTaxRatesKey),
        `${parameterSetPath(centralTaxRatesKey)}?version=2020-04-01&q=transport&page=2`,
        parameterSetPath("gst.no-such-set"),
      ]) {
        const response = await page.goto(path);

        expect(response?.status(), path).toBe(404);
        await expect(page.getByRole("heading", { name: "This page does not exist" }), path).toBeVisible();
      }
    });
  });

  test("the API answers the sets and a set with the disabled module's problem", async ({ api }) => {
    for (const { path, query } of [
      { path: parameterSetsApi, query: "" },
      { path: parameterSetApi(centralTaxRatesKey), query: "?on=2020-04-01" },
    ]) {
      const response = await api.get(`${path}${query}`);

      expect(response.status(), path).toBe(404);
      expect(response.headers()["content-type"], path).toContain("application/problem+json");
      expect(await response.json(), path).toMatchObject({ code: "feature.disabled", instance: path });
    }
  });
});
