import { randomUUID } from "node:crypto";

import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import {
  ExchangeRatePage,
  NewExchangeRatePage,
} from "../../../pages/platform/reference-data/exchange-rate.page";
import { ExchangeRatesPage } from "../../../pages/platform/reference-data/exchange-rates.page";
import {
  ImportExchangeRatesPage,
  ratesCsv,
} from "../../../pages/platform/reference-data/import-exchange-rates.page";

test.describe("exchange rates without the API", () => {
  test.skip(
    !offlineBaseURL,
    "E2E_OFFLINE_BASE_URL is not set and Playwright did not start the offline web instance",
  );
  test.use({
    baseURL: offlineBaseURL ?? "",
    persona: null,
    expectedConsoleError: /the server responded with a status of 500/,
  });
  test.beforeEach(async ({ context }) => {
    await addUnverifiedSessionCookie(context, offlineBaseURL ?? "");
  });

  forEachTheme("explains that the list is unavailable and offers no filters", async ({ page, capture }) => {
    const listPage = new ExchangeRatesPage(page);
    const renewal = sessionRenewed(page);
    await listPage.goto();
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle("Exchange rates · ERP-AI-Pro");
    await expect(listPage.listCard).toBeVisible();
    await expect(listPage.unavailable).toHaveRole("status");
    await expect(listPage.unavailable).toContainText("Exchange rates are not available.");
    await expect(listPage.unavailable).toContainText("The API did not respond.");
    await expect(listPage.list.filters).toHaveCount(0);
    await expect(listPage.list.table).toHaveCount(0);
    await expect(listPage.list.cardList).toHaveCount(0);
    await expect(listPage.recordLink).toBeVisible();
    await expect(listPage.importLink).toBeVisible();
    await capture("exchange-rates-unavailable");
  });

  forEachTheme("explains that the rate's details are not available", async ({ page, capture }) => {
    const ratePage = new ExchangeRatePage(page);
    const renewal = sessionRenewed(page);
    await ratePage.goto(randomUUID());
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle("Exchange rate · ERP-AI-Pro");
    await expect(ratePage.heading).toHaveText("Exchange rate");
    await expect(ratePage.unavailable).toHaveRole("status");
    await expect(ratePage.unavailable).toContainText("This exchange rate's details are not available.");
    await expect(ratePage.unavailable).toContainText("The API did not respond.");
    await expect(ratePage.details).toHaveCount(0);
    await expect(ratePage.actions).toHaveCount(0);
    await expect(ratePage.history).toHaveCount(0);
    await capture("exchange-rate-unavailable");
  });

  forEachTheme(
    "offers the record page, saying the currencies cannot be listed",
    async ({ page, capture }) => {
      const newPage = new NewExchangeRatePage(page);
      const renewal = sessionRenewed(page);
      await newPage.goto();
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle("Record a rate · ERP-AI-Pro");
      await newPage.currency.fill("USD");
      const unreadable = "The list of currencies could not be read. Try again later.";
      await expect(page.getByRole("status").filter({ hasText: unreadable })).toHaveText(unreadable);
      await capture("new-exchange-rate-unavailable");
    },
  );

  forEachTheme("checks a chosen file without the API", async ({ page, capture }) => {
    const importPage = new ImportExchangeRatesPage(page);
    const renewal = sessionRenewed(page);
    await importPage.goto();
    expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

    await expect(page).toHaveTitle("Import rates · ERP-AI-Pro");
    await importPage.waitUntilInteractive();
    await importPage.choose(ratesCsv("rates.csv", ["date,currency,unit,rate", "01-04-2026,USD,1,85"]));
    await expect(importPage.status).toHaveText("rates.csv holds 1 rate, ready to import.");
    await expect(importPage.importButton).toHaveText("Import 1 rate");
    await capture("import-exchange-rates-unavailable");
  });
});
