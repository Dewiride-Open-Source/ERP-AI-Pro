import type { Page, Request } from "@playwright/test";
import { randomUUID } from "node:crypto";

import { withholdClientBundles } from "../../../fixtures/client-bundles";
import {
  exchangeRateApi,
  exchangeRateImportsApi,
  exchangeRatesApi,
  rateDays,
  readRate,
  recordRate,
  removeRates,
  typedDate,
} from "../../../fixtures/records/exchange-rate-records";
import { assignRoles } from "../../../fixtures/records/person-records";
import { expect, forEachTheme, test, themes } from "../../../fixtures/test";
import {
  ExchangeRatePage,
  isRecordingRequest,
  isServerFunctionCall,
  NewExchangeRatePage,
} from "../../../pages/platform/reference-data/exchange-rate.page";
import {
  exchangeRatePath,
  exchangeRatesPath,
  ExchangeRatesPage,
  importExchangeRatesPath,
  newExchangeRatePath,
  ratesOf,
} from "../../../pages/platform/reference-data/exchange-rates.page";
import { ImportExchangeRatesPage } from "../../../pages/platform/reference-data/import-exchange-rates.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const recordedPath = /^\/platform\/reference-data\/exchange-rates\/([0-9a-f-]{36})$/;

const noAccess = "You do not have access to this page";

const laterDay = /^Enter a date on or before \d{2}-\d{2}-\d{4}\.$/;

function watch(page: Page, matches: (request: Request) => boolean): { count: () => number } {
  let count = 0;
  page.on("request", (request) => {
    if (matches(request)) count += 1;
  });
  return { count: () => count };
}

// Each test below records rates of a currency no other test uses, on days of its own window (rateDays).
test.describe("exchange rates", () => {
  test.use({ persona: "administrator" });

  test.describe("before the pages are interactive", () => {
    test("keep the list, the forms and the file chooser inert so no click is lost", async ({ page }) => {
      const listPage = new ExchangeRatesPage(page);
      const newPage = new NewExchangeRatePage(page);
      const importPage = new ImportExchangeRatesPage(page);
      await withholdClientBundles(page);

      await page.goto(exchangeRatesPath);
      await expect(listPage.heading).toBeVisible();
      await expect(listPage.list.root).toHaveAttribute("data-hydrating", "");

      await page.goto(newExchangeRatePath);
      await expect(newPage.heading).toBeVisible();
      await expect(newPage.save).toBeDisabled();
      await expect(newPage.save).toHaveAttribute("data-hydrating", "");

      await page.goto(importExchangeRatesPath);
      await expect(importPage.heading).toBeVisible();
      await expect(importPage.template).toBeDisabled();
      await expect(importPage.chooseFile).toBeDisabled();
    });
  });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("records a rate after refusing an empty form, refuses a second rate of that day and finds it in the list", async ({
        page,
        api,
        person,
        capture,
      }, testInfo) => {
        test.slow();
        const listPage = new ExchangeRatesPage(page);
        const newPage = new NewExchangeRatePage(page);
        const ratePage = new ExchangeRatePage(page);
        const shell = new AppShell(page);
        const [day = ""] = rateDays(testInfo, theme, 1);
        const name = `CHF on ${typedDate(day)}`;
        const sent = watch(page, isRecordingRequest);
        await removeRates(api, "CHF", [day]);
        try {
          await listPage.goto();
          await listPage.waitUntilInteractive();
          await expect(page).toHaveTitle("Exchange rates · ERP-AI-Pro");
          await expect(listPage.unavailable).toHaveCount(0);
          await expect(listPage.recordLink).toHaveAttribute("href", newExchangeRatePath);
          await expect(listPage.importLink).toHaveAttribute("href", importExchangeRatesPath);
          await expect(listPage.list.filters).toMatchAriaSnapshot(`
            - search "Filter exchange rates":
              - group:
                - text: Currency
                - button "Currency Any"
              - group:
                - text: Days between From
                - group:
                  - textbox "Days between From"
                - text: To
                - group:
                  - textbox "Days between To"
                - button "Choose dates"
              - button "Apply filters"
          `);
          await capture("exchange-rates");

          await listPage.recordLink.click();
          await expect(newPage.heading).toBeVisible();
          await expect(newPage.save).toBeEnabled();
          await expect(page).toHaveTitle("Record a rate · ERP-AI-Pro");
          await expect(newPage.form).toMatchAriaSnapshot(`
            - form "The rate":
              - group:
                - text: Currency
                - group:
                  - combobox "Currency"
                  - group:
                    - button "Show options Currency"
                - paragraph: A foreign currency; type its code or name.
              - group:
                - text: Day
                - group:
                  - textbox "Day"
                  - group:
                    - button "Choose date"
                - paragraph: "The day the rate is for: today or an earlier day."
              - group:
                - text: Rate
                - group:
                  - group: ₹
                  - textbox "Rate"
                - paragraph: Rupees for the units of the currency, with up to 4 digits after the decimal point.
              - group:
                - text: Units
                - combobox "Units": "1"
                - paragraph: How many units of the currency the rate is for, such as 100 for the yen.
              - group:
                - text: Source
                - textbox "Source"
                - paragraph: Optional. Where the rate comes from, such as FBIL reference rate; up to 200 characters.
              - button "Record the rate"
              - link "Cancel":
                - /url: ${exchangeRatesPath}
          `);
          await expect(newPage.currency).toHaveAttribute("aria-required", "true");
          await capture("new-exchange-rate");

          await newPage.save.click();
          await expect(newPage.currency).toBeFocused();
          await expect(newPage.currency).toHaveAttribute("aria-invalid", "true");
          await expect(newPage.fieldOf(newPage.currency)).toContainText("Choose the currency.");
          await expect(newPage.fieldOf(newPage.day)).toContainText("Enter the day the rate is for.");
          await expect(newPage.fieldOf(newPage.rate)).toContainText("Enter the rate.");
          expect(sent.count(), "Server Function calls").toBe(0);
          await capture("new-exchange-rate-refused", newPage.form);

          await newPage.dayTrigger.focus();
          await page.keyboard.press("Enter");
          await expect(newPage.dayCalendar).toBeVisible();
          await page.keyboard.press("Escape");
          await expect(newPage.dayCalendar).toBeHidden();
          await expect(newPage.dayTrigger).toBeFocused();

          await newPage.day.fill("01-01-2999");
          await expect(newPage.fieldOf(newPage.day).getByText(laterDay)).toBeVisible();
          await newPage.fill({
            code: "CHF",
            option: "CHF Swiss Franc",
            day: typedDate(day),
            rate: "95.1234",
            source: "Test desk, Zürich",
          });
          await newPage.chooseUnit("100");
          await newPage.save.click();

          await expect(shell.toast(`Recorded the rate of CHF for ${typedDate(day)}.`)).toBeVisible();
          await expect(page).toHaveURL((url) => recordedPath.test(url.pathname));
          const id = recordedPath.exec(new URL(page.url()).pathname)?.[1] ?? "";
          await expect(ratePage.heading).toHaveText(name);
          await expect(page).toHaveTitle(`${name} · ERP-AI-Pro`);
          await expect(ratePage.details).toMatchAriaSnapshot(`
            - heading "Details" [level=2]
            - text: The rate is in rupees for 100 CHF on ${typedDate(day)}, as its source published it.
            - term: Currency
            - definition: CHF
            - term: Day
            - definition: ${typedDate(day)}
            - term: Rate
            - definition: ₹95.1234 for 100 CHF
            - term: Source
            - definition: Test desk, Zürich
          `);
          expect((await readRate(api, id)).unit, "the unit recorded").toBe(100);
          await expect(ratePage.history.getByTestId("audit-change-summary")).toHaveText([
            `${person?.name ?? ""} created the exchange rate ${name}.`,
          ]);
          await expect(ratePage.historyLink).toHaveAttribute("href", `/platform/audit?record=${id}`);
          await capture("exchange-rate");

          await newPage.goto();
          await newPage.fill({ code: "CHF", option: "CHF Swiss Franc", day: typedDate(day), rate: "96" });
          await newPage.save.click();
          await expect(newPage.summary).toBeFocused();
          await expect(newPage.summary).toContainText(
            `A rate of CHF is already recorded for ${typedDate(day)}; correct that one instead.`,
          );
          await expect(page).toHaveURL((url) => url.pathname === newExchangeRatePath);

          await listPage.goto(ratesOf(["CHF"], day, day));
          await expect(listPage.list.status).toHaveText("Showing 1 exchange rate.");
          await expect(listPage.rateLink(name)).toHaveAttribute("href", exchangeRatePath(id));
        } finally {
          await removeRates(api, "CHF", [day]);
        }
      });

      test("lists rates by currency and days, the newest day first, a page at a time", async ({
        page,
        api,
        capture,
      }, testInfo) => {
        test.slow();
        const listPage = new ExchangeRatesPage(page);
        const { list } = listPage;
        const days = rateDays(testInfo, theme, 11);
        const first = days[0] ?? "";
        const last = days.at(-1) ?? "";
        await removeRates(api, "SEK", days);
        await removeRates(api, "NOK", days);
        try {
          const latest = await recordRate(api, {
            currency: "SEK",
            rateDate: last,
            rate: 8.1,
            source: "Test desk",
          });
          for (const [index, day] of days.slice(0, -1).entries()) {
            await recordRate(api, { currency: "SEK", rateDate: day, rate: 8 + index / 100 });
          }
          await recordRate(api, { currency: "NOK", rateDate: first, rate: 7.5, unit: 1 });

          await listPage.goto();
          await listPage.waitUntilInteractive();
          await list.chooseOptions("Currency", ["SEK Swedish Krona"]);
          await list.rangeStart("Days between").fill(typedDate(first));
          await list.rangeEnd("Days between").fill(typedDate(last));
          await list.applyFilters.click();

          await expect(page).toHaveURL(
            (url) => url.pathname === exchangeRatesPath && url.search === ratesOf(["SEK"], first, last),
          );
          await expect(list.status).toHaveText(
            "Showing 1–10 of 11 exchange rates. Sorted by Day, latest first.",
          );
          await expect.poll(() => list.titles()).toEqual(days.slice(1).reverse().map(typedDate));
          const latestName = `SEK on ${typedDate(last)}`;
          if (await list.showsCards()) {
            await expect(listPage.row(latestName)).toMatchAriaSnapshot(`
              - listitem:
                - link "${latestName}":
                  - /url: ${exchangeRatePath(latest.id)}
                - term: Currency
                - definition: SEK
                - term: Rate
                - definition: ₹8.1000
                - term: Per
                - definition: "1"
                - term: Source
                - definition: Test desk
            `);
          } else {
            await expect(list.columnHeader("Day")).toHaveAttribute("aria-sort", "descending");
            await expect(listPage.row(latestName)).toMatchAriaSnapshot(`
              - row:
                - rowheader "${latestName}":
                  - link "${latestName}":
                    - /url: ${exchangeRatePath(latest.id)}
                - cell "SEK"
                - cell "₹8.1000"
                - cell "1"
                - cell "Test desk"
            `);
          }
          await capture("exchange-rates-found", listPage.listCard);

          await list.nextPage.click();
          await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
          await expect(list.status).toHaveText(
            "Showing 11–11 of 11 exchange rates. Sorted by Day, latest first.",
          );
          await expect.poll(() => list.titles()).toEqual([typedDate(first)]);

          await list.sortBy("Day", "Day, earliest first");
          await expect(page).toHaveURL(
            (url) => url.searchParams.get("sort") === "rateDate:asc" && !url.searchParams.has("page"),
          );
          await expect(list.status).toHaveText(
            "Showing 1–10 of 11 exchange rates. Sorted by Day, earliest first.",
          );
          await expect.poll(() => list.titles()).toEqual(days.slice(0, 10).map(typedDate));

          await list.chooseOptions("Currency", ["NOK Norwegian Krone"]);
          await list.applyFilters.click();
          await expect(page).toHaveURL((url) => url.searchParams.getAll("currency").join(",") === "NOK,SEK");
          await expect(list.status).toHaveText(
            "Showing 1–10 of 12 exchange rates. Sorted by Day, earliest first.",
          );
          await list.choosePageSize(20);
          await expect(page).toHaveURL((url) => url.searchParams.get("size") === "20");
          await expect(list.status).toHaveText(
            "Showing all 12 exchange rates. Sorted by Day, earliest first.",
          );
          await expect.poll(() => list.titles()).toEqual([typedDate(first), ...days.map(typedDate)]);

          const firstNok = listPage.row(`NOK on ${typedDate(first)}`);
          await list.setColumnVisible("Source", false);
          await expect(page).toHaveURL((url) => url.searchParams.get("hide") === "source");
          if (await list.showsCards()) {
            await expect(firstNok.getByRole("term")).toHaveText(["Currency", "Rate", "Per"]);
          } else {
            await expect(list.columnHeader("Source")).toHaveCount(0);
            await expect(firstNok.getByRole("cell")).toHaveText(["NOK", "₹7.5000", "1"]);
          }
          await list.setColumnVisible("Source", true);
          await expect(page).toHaveURL(
            (url) => !url.searchParams.has("hide") && url.searchParams.get("size") === "20",
          );
          if (await list.showsCards()) {
            await expect(firstNok.getByRole("term")).toHaveText(["Currency", "Rate", "Per", "Source"]);
          } else {
            await expect(list.columnHeader("Source")).toBeVisible();
          }

          const rangeCalendar = page.getByRole("dialog", { name: "Choose dates", exact: true });
          await list.rangeTrigger.focus();
          await page.keyboard.press("Enter");
          await expect(rangeCalendar).toBeVisible();
          await page.keyboard.press("Escape");
          await expect(rangeCalendar).toBeHidden();
          await expect(list.rangeTrigger).toBeFocused();

          await listPage.goto(ratesOf(["NOK"], last, last));
          await listPage.waitUntilInteractive();
          await expect(list.noMatches).toContainText("No exchange rates match these filters.");
          await expect(list.status).toHaveText("No exchange rates to show.");
          await capture("exchange-rates-no-matches", listPage.listCard);
          await list.noMatches.getByRole("link", { name: "Clear filters" }).click();
          await expect(page).toHaveURL((url) => url.pathname === exchangeRatesPath && url.search === "");
          await expect(list.noMatches).toHaveCount(0);
          await expect(list.optionsFilter("Currency")).toHaveAccessibleName("Currency Any");
        } finally {
          await removeRates(api, "SEK", days);
          await removeRates(api, "NOK", days);
        }
      });

      test("shows a rate with its history, corrects it, offers the latest rate after a change elsewhere and removes it", async ({
        page,
        api,
        person,
        capture,
      }, testInfo) => {
        test.slow();
        const ratePage = new ExchangeRatePage(page);
        const listPage = new ExchangeRatesPage(page);
        const shell = new AppShell(page);
        const [day = ""] = rateDays(testInfo, theme, 1);
        const name = `DKK on ${typedDate(day)}`;
        await removeRates(api, "DKK", [day]);
        try {
          const recorded = await recordRate(api, {
            currency: "DKK",
            rateDate: day,
            rate: 12.3456,
            source: "Test desk",
          });
          const corrections = watch(page, (request) =>
            isServerFunctionCall(request, exchangeRatePath(recorded.id)),
          );

          await ratePage.goto(recorded.id);
          await ratePage.waitUntilInteractive();
          await expect(page).toHaveTitle(`${name} · ERP-AI-Pro`);
          await expect(ratePage.header).toMatchAriaSnapshot(`
            - paragraph: Exchange rates
            - heading "${name}" [level=1]
            - button "Correct"
            - button "Remove"
          `);
          await expect(ratePage.details).toContainText("₹12.3456 for 1 DKK");

          await ratePage.correct.click();
          await expect(ratePage.correctDialog).toBeVisible();
          await expect(ratePage.correctDialog).toMatchAriaSnapshot(`
            - dialog "Correct the rate of ${name}":
              - heading "Correct the rate of ${name}" [level=2]
              - paragraph: Change the rate, the units it is for or its source. The currency and the day stay; for another currency or day, remove this rate and record a new one.
              - group:
                - text: Rate
                - group:
                  - group: ₹
                  - textbox "Rate": "12.3456"
              - group:
                - text: Units
                - combobox "Units": "1"
              - group:
                - text: Source
                - textbox "Source": Test desk
              - button "Cancel"
              - button "Save the correction"
              - button "Close"
          `);
          await capture("exchange-rate-correct", ratePage.correctDialog);

          await ratePage.correctRate.fill("99");
          await ratePage.correctCancel.click();
          await expect(ratePage.correctDialog).toBeHidden();
          await expect(ratePage.correct).toBeFocused();
          await expect(ratePage.details).toContainText("₹12.3456 for 1 DKK");
          expect(corrections.count(), "Server Function calls").toBe(0);

          await ratePage.correct.click();
          await expect(ratePage.correctRate, "the dialog opens on the rate the page shows").toHaveValue(
            "12.3456",
          );
          await ratePage.correctClose.click();
          await expect(ratePage.correctDialog).toBeHidden();
          await expect(ratePage.correct).toBeFocused();
          expect(corrections.count(), "Server Function calls").toBe(0);

          await ratePage.correct.click();
          await expect(ratePage.correctDialog).toBeVisible();
          await ratePage.correctRate.fill("0");
          await ratePage.correctSave.click();
          await expect(ratePage.correctRate).toBeFocused();
          await expect(ratePage.fieldOf(ratePage.correctRate)).toContainText("Enter a rate of more than ₹0.");
          expect(corrections.count(), "Server Function calls").toBe(0);

          await ratePage.correctRate.fill("13.5");
          await ratePage.chooseUnit("10");
          await ratePage.correctSource.fill("Corrected desk");
          await ratePage.correctSave.click();
          await expect(shell.toast(`Corrected the rate of ${name}.`)).toBeVisible();
          await expect(ratePage.correctDialog).toBeHidden();
          await expect(ratePage.correct).toBeFocused();
          await expect(ratePage.details).toContainText("₹13.5000 for 10 DKK");
          await expect(ratePage.details).toContainText("Corrected desk");
          await expect(ratePage.history.getByTestId("audit-change-summary")).toHaveText([
            `${person?.name ?? ""} changed the exchange rate ${name}.`,
            `${person?.name ?? ""} created the exchange rate ${name}.`,
          ]);
          await capture("exchange-rate-corrected");

          await ratePage.correct.click();
          await expect(ratePage.correctRate).toHaveValue("13.5000");
          const current = await readRate(api, recorded.id);
          const changedElsewhere = await api.put(exchangeRateApi(recorded.id), {
            data: { rate: 14, unit: 10, source: "Changed elsewhere", version: current.version },
          });
          expect(changedElsewhere.status(), "the correction made elsewhere").toBe(200);
          await ratePage.correctRate.fill("15");
          await ratePage.correctSave.click();
          await expect(ratePage.correctSummary).toBeFocused();
          await expect(ratePage.correctSummary).toContainText(
            "Someone changed this rate after the page was loaded. Show the latest rate, then correct it again.",
          );
          await ratePage.showLatest.click();
          await expect(ratePage.correctDialog).toBeHidden();
          await expect(ratePage.details).toContainText("₹14.0000 for 10 DKK");
          await expect(ratePage.details).toContainText("Changed elsewhere");

          const confirmation = ratePage.confirmation(`Remove the rate of ${name}?`);
          await ratePage.remove.click();
          await expect(confirmation).toBeVisible();
          await confirmation.getByRole("button", { name: "Keep the rate", exact: true }).click();
          await expect(confirmation).toBeHidden();
          await expect(ratePage.remove).toBeFocused();
          await expect(ratePage.heading).toHaveText(name);
          expect((await api.get(exchangeRateApi(recorded.id))).status(), "the rate kept").toBe(200);

          await ratePage.remove.click();
          await expect(confirmation).toBeVisible();
          await expect(confirmation).toContainText(
            `The rate leaves the list, and its history stays in the audit trail. A rate of DKK can then be recorded for ${typedDate(day)} again.`,
          );
          await capture("exchange-rate-remove", confirmation);
          await confirmation.getByRole("button", { name: "Remove the rate", exact: true }).click();

          await expect(shell.toast(`Removed the rate of ${name}.`)).toBeVisible();
          await expect(page).toHaveURL((url) => url.pathname === exchangeRatesPath);
          await expect(listPage.heading).toBeVisible();
          expect((await api.get(exchangeRateApi(recorded.id))).status(), "the removed rate").toBe(404);
        } finally {
          await removeRates(api, "DKK", [day]);
        }
      });

      test("says a rate removed elsewhere can no longer be corrected, and counts removing it again as done", async ({
        page,
        api,
        capture,
      }, testInfo) => {
        test.slow();
        const ratePage = new ExchangeRatePage(page);
        const listPage = new ExchangeRatesPage(page);
        const shell = new AppShell(page);
        const days = rateDays(testInfo, theme, 2);
        const [correctedDay = "", removedDay = ""] = days;
        const removedName = `PLN on ${typedDate(removedDay)}`;
        await removeRates(api, "PLN", days);
        try {
          const corrected = await recordRate(api, { currency: "PLN", rateDate: correctedDay, rate: 21.5 });
          const removed = await recordRate(api, { currency: "PLN", rateDate: removedDay, rate: 21.75 });

          await ratePage.goto(corrected.id);
          await ratePage.waitUntilInteractive();
          await ratePage.correct.click();
          await expect(ratePage.correctDialog).toBeVisible();
          const removedElsewhere = await api.delete(exchangeRateApi(corrected.id));
          expect(removedElsewhere.status(), "the removal made elsewhere").toBe(204);
          await ratePage.correctRate.fill("22");
          await ratePage.correctSave.click();
          await expect(ratePage.correctSummary).toBeFocused();
          await expect(ratePage.correctSummary).toContainText(
            "This rate was removed after the page was loaded, so it can no longer be corrected.",
          );
          await expect(ratePage.showLatest).toHaveCount(0);
          await capture("exchange-rate-correct-removed", ratePage.correctDialog);
          await ratePage.correctCancel.click();
          await expect(ratePage.correctDialog).toBeHidden();

          await ratePage.goto(removed.id);
          await ratePage.waitUntilInteractive();
          const removedFirst = await api.delete(exchangeRateApi(removed.id));
          expect(removedFirst.status(), "the removal made elsewhere").toBe(204);
          const confirmation = ratePage.confirmation(`Remove the rate of ${removedName}?`);
          await ratePage.remove.click();
          await expect(confirmation).toBeVisible();
          await confirmation.getByRole("button", { name: "Remove the rate", exact: true }).click();

          await expect(shell.toast(`Removed the rate of ${removedName}.`)).toContainText(
            "It had already been removed.",
          );
          await expect(page).toHaveURL((url) => url.pathname === exchangeRatesPath);
          await expect(listPage.heading).toBeVisible();
        } finally {
          await removeRates(api, "PLN", days);
        }
      });
    });
  }

  test("the record page's Cancel goes back to the list and records nothing", async ({ page }) => {
    const listPage = new ExchangeRatesPage(page);
    const newPage = new NewExchangeRatePage(page);
    const sent = watch(page, isRecordingRequest);

    await newPage.goto();
    await newPage.rate.fill("1");
    await newPage.cancel.click();

    await expect(page).toHaveURL((url) => url.pathname === exchangeRatesPath);
    await expect(listPage.heading).toBeVisible();
    expect(sent.count(), "Server Function calls").toBe(0);
  });

  test.describe("that does not exist", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    test("answers the not-found page, and the API its own problem", async ({ page, api }) => {
      for (const path of [exchangeRatePath(randomUUID()), exchangeRatePath("not-a-rate")]) {
        const response = await page.goto(path);

        expect(response?.status(), path).toBe(404);
        await expect(page.getByRole("heading", { name: "This page does not exist" }), path).toBeVisible();
      }

      const missing = exchangeRateApi(randomUUID());
      const answer = await api.get(missing);
      expect(answer.status()).toBe(404);
      expect(await answer.json()).toMatchObject({ code: "exchange-rate.not-found", instance: missing });
    });
  });
});

test.describe("exchange rates for an employee", () => {
  forEachTheme(
    "leaves the entry out of the navigation and tells them they have no access to the pages",
    async ({ page, capture }) => {
      const shell = new AppShell(page);
      const listPage = new ExchangeRatesPage(page);

      await page.goto(exchangeRatesPath);
      await expect(page.getByRole("heading", { name: noAccess, level: 1 })).toBeVisible();
      await expect(page).toHaveTitle("No access · ERP-AI-Pro");
      await expect(listPage.listCard).toHaveCount(0);
      await expect(listPage.recordLink).toHaveCount(0);
      await capture("exchange-rates-no-access");
      await shell.openNavigation();
      await expect(shell.primaryNavigation).toBeVisible();
      await expect(shell.navigationLink("Exchange rates")).toHaveCount(0);
      await shell.closeDrawer();

      for (const path of [newExchangeRatePath, importExchangeRatesPath, exchangeRatePath(randomUUID())]) {
        await page.goto(path);

        await expect(page.getByRole("heading", { name: noAccess, level: 1 }), path).toBeVisible();
        await expect(page, path).toHaveTitle("No access · ERP-AI-Pro");
      }
    },
  );

  test("the API refuses them the list, a rate, a recording, an import, a correction and a removal", async ({
    api,
  }) => {
    const id = randomUUID();
    const rate = { currency: "USD", rateDate: "1901-01-01", rate: 1, unit: 1 };
    const answers = [
      { request: "the list", response: await api.get(exchangeRatesApi) },
      { request: "a rate", response: await api.get(exchangeRateApi(id)) },
      {
        request: "a recording",
        response: await api.post(exchangeRatesApi, {
          headers: { "Idempotency-Key": randomUUID() },
          data: rate,
        }),
      },
      {
        request: "an import",
        response: await api.post(exchangeRateImportsApi, {
          headers: { "Idempotency-Key": randomUUID() },
          data: { rates: [rate] },
        }),
      },
      {
        request: "a correction",
        response: await api.put(exchangeRateApi(id), { data: { rate: 1, unit: 1, version: "AAAAAAAAB9E=" } }),
      },
      { request: "a removal", response: await api.delete(exchangeRateApi(id)) },
    ];

    for (const { request, response } of answers) {
      expect(response.status(), request).toBe(403);
      expect(await response.json(), request).toMatchObject({ code: "request.forbidden" });
    }
  });
});

test.describe("exchange rates for a finance person", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("finds the entry, records a rate and corrects it once an administrator gives them the Finance role", async ({
        page,
        api,
        person,
        request,
        capture,
      }, testInfo) => {
        test.slow();
        const shell = new AppShell(page);
        const listPage = new ExchangeRatesPage(page);
        const newPage = new NewExchangeRatePage(page);
        const ratePage = new ExchangeRatePage(page);
        const [day = ""] = rateDays(testInfo, theme, 1);
        const name = `CZK on ${typedDate(day)}`;
        await assignRoles(request, person, ["finance"]);
        await removeRates(api, "CZK", [day]);
        try {
          await page.goto("/");
          await shell.waitUntilInteractive();
          await shell.openNavigation();
          await shell.navigationLink("Exchange rates").click();
          await expect(page).toHaveURL((url) => url.pathname === exchangeRatesPath);
          await expect(listPage.heading).toBeVisible();
          await expect(listPage.listDescription, "a rate's changes are an administrator's").toHaveText(
            "The rupees for foreign currencies, the newest day first. Open a rate to see its details.",
          );

          await listPage.recordLink.click();
          await expect(newPage.heading).toBeVisible();
          await expect(newPage.save).toBeEnabled();
          await newPage.fill({ code: "CZK", option: "CZK Czech Koruna", day: typedDate(day), rate: "3.5" });
          await newPage.save.click();
          await expect(shell.toast(`Recorded the rate of CZK for ${typedDate(day)}.`)).toBeVisible();
          await expect(ratePage.heading).toHaveText(name);
          await expect(ratePage.historyOrItsLoading, "the audit trail is an administrator's").toHaveCount(0);

          await ratePage.waitUntilInteractive();
          await ratePage.correct.click();
          await ratePage.correctRate.fill("3.75");
          await ratePage.correctSave.click();
          await expect(shell.toast(`Corrected the rate of ${name}.`)).toBeVisible();
          await expect(ratePage.correctDialog).toBeHidden();
          await expect(ratePage.details).toContainText("₹3.7500 for 1 CZK");
          await expect(ratePage.historyOrItsLoading, "the audit trail is an administrator's").toHaveCount(0);
          await capture("exchange-rate-finance");
        } finally {
          await removeRates(api, "CZK", [day]);
        }
      });

      test("is told a rate was not removed when an administrator took the Finance role away while its page was open", async ({
        page,
        api,
        person,
        request,
        capture,
      }, testInfo) => {
        test.slow();
        const ratePage = new ExchangeRatePage(page);
        const [day = ""] = rateDays(testInfo, theme, 1);
        const name = `HUF on ${typedDate(day)}`;
        await assignRoles(request, person, ["finance"]);
        await removeRates(api, "HUF", [day]);
        try {
          const recorded = await recordRate(api, { currency: "HUF", rateDate: day, rate: 0.2468 });
          await ratePage.goto(recorded.id);
          await ratePage.waitUntilInteractive();
          await assignRoles(request, person, []);

          const confirmation = ratePage.confirmation(`Remove the rate of ${name}?`);
          await ratePage.remove.click();
          await expect(confirmation).toBeVisible();
          await confirmation.getByRole("button", { name: "Remove the rate", exact: true }).click();
          await expect(confirmation).toBeHidden();
          await expect(ratePage.removeError).toHaveText("You do not have permission to do this.");
          await expect(ratePage.remove).toBeFocused();
          await expect(ratePage.heading).toHaveText(name);
          await capture("exchange-rate-remove-refused", ratePage.actions);
          expect((await request.get(exchangeRateApi(recorded.id))).status(), "the rate kept").toBe(200);
        } finally {
          await assignRoles(request, person, ["finance"]);
          await removeRates(api, "HUF", [day]);
        }
      });
    });
  }
});
