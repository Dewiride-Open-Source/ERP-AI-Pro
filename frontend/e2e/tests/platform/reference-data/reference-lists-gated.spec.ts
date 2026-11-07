import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import {
  countriesPath,
  currenciesPath,
  financialYearsPath,
  indianStatesPath,
  referenceListsApi,
} from "../../../pages/platform/reference-data/reference-lists.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// An administrator holds every permission of the module, so only the disabled module can hide its pages from them.
test.describe("reference lists with the reference data module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "", persona: "administrator" });

  test.describe("their pages", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("hide the entry and answer not found inside the shell", async ({ page, capture }) => {
      const shell = new AppShell(page);

      const response = await page.goto(countriesPath);

      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await expect(page.getByRole("navigation", { name: "Reference lists" })).toHaveCount(0);
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Reference lists")).toHaveCount(0);
      await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
      await shell.closeDrawer();
      await capture("reference-lists-disabled");
    });

    test("answer not found for every list and every address of one", async ({ page }) => {
      for (const path of [
        indianStatesPath,
        currenciesPath,
        financialYearsPath,
        `${countriesPath}?q=india&page=2`,
        `${financialYearsPath}?year=2025-26`,
      ]) {
        const response = await page.goto(path);

        expect(response?.status(), path).toBe(404);
        await expect(page.getByRole("heading", { name: "This page does not exist" }), path).toBeVisible();
      }
    });
  });

  test("the API answers every list with the disabled module's problem", async ({ api }) => {
    for (const { path, query } of [
      { path: referenceListsApi.countries, query: "" },
      { path: referenceListsApi.indianStates, query: "?on=2020-07-31" },
      { path: referenceListsApi.currencies, query: "" },
      { path: referenceListsApi.financialYears, query: "?from=2024-25&to=2027-28" },
    ]) {
      const response = await api.get(`${path}${query}`);

      expect(response.status(), path).toBe(404);
      expect(response.headers()["content-type"], path).toContain("application/problem+json");
      expect(await response.json(), path).toMatchObject({ code: "feature.disabled", instance: path });
    }
  });
});
