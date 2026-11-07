import { randomUUID } from "node:crypto";

import {
  exchangeRateApi,
  exchangeRateImportsApi,
  exchangeRatesApi,
} from "../../../fixtures/records/exchange-rate-records";
import { gatedBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import {
  exchangeRatePath,
  exchangeRatesPath,
  importExchangeRatesPath,
  newExchangeRatePath,
} from "../../../pages/platform/reference-data/exchange-rates.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// An administrator holds every permission of the module, so only the disabled module can hide its pages from them.
test.describe("exchange rates with the reference data module disabled", () => {
  test.skip(
    !gatedBaseURL,
    "E2E_GATED_BASE_URL is not set and Playwright did not start the gated web instance",
  );
  test.use({ baseURL: gatedBaseURL ?? "", persona: "administrator" });

  test.describe("its pages", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("hide the module and answer not found inside the shell", async ({ page, capture }) => {
      const shell = new AppShell(page);

      const response = await page.goto(exchangeRatesPath);

      expect(response?.status()).toBe(404);
      await expect(shell.banner).toBeVisible();
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Exchange rates")).toHaveCount(0);
      await expect(shell.primaryNavigation.getByRole("link")).toHaveText(["Home"]);
      await shell.closeDrawer();
      await capture("exchange-rates-disabled");
    });

    test("answer not found for the record, import and rate pages too", async ({ page }) => {
      for (const path of [newExchangeRatePath, importExchangeRatesPath, exchangeRatePath(randomUUID())]) {
        const response = await page.goto(path);

        expect(response?.status(), path).toBe(404);
        await expect(page.getByRole("heading", { name: "This page does not exist" }), path).toBeVisible();
      }
    });
  });

  test("the API answers the list, a rate, a recording, an import, a correction and a removal with the disabled module's problem", async ({
    api,
  }) => {
    const rate = exchangeRateApi(randomUUID());
    const recording = { currency: "USD", rateDate: "1901-01-01", rate: 1, unit: 1 };
    const answers = [
      { path: exchangeRatesApi, response: await api.get(exchangeRatesApi) },
      { path: rate, response: await api.get(rate) },
      {
        path: exchangeRatesApi,
        response: await api.post(exchangeRatesApi, {
          headers: { "Idempotency-Key": randomUUID() },
          data: recording,
        }),
      },
      {
        path: exchangeRateImportsApi,
        response: await api.post(exchangeRateImportsApi, {
          headers: { "Idempotency-Key": randomUUID() },
          data: { rates: [recording] },
        }),
      },
      {
        path: rate,
        response: await api.put(rate, { data: { rate: 1, unit: 1, version: "AAAAAAAAB9E=" } }),
      },
      { path: rate, response: await api.delete(rate) },
    ];

    for (const { path, response } of answers) {
      expect(response.status(), path).toBe(404);
      expect(response.headers()["content-type"], path).toContain("application/problem+json");
      expect(await response.json(), path).toMatchObject({ code: "feature.disabled", instance: path });
    }
  });
});
