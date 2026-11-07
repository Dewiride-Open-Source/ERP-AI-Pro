import {
  exchangeRatesApi,
  rateDays,
  removeRates,
  typedDate,
} from "../../../fixtures/records/exchange-rate-records";
import { expect, forEachTheme, test, themes } from "../../../fixtures/test";
import { ExchangeRatePage } from "../../../pages/platform/reference-data/exchange-rate.page";
import {
  exchangeRatesPath,
  ExchangeRatesPage,
  ratesOf,
} from "../../../pages/platform/reference-data/exchange-rates.page";
import {
  byteOrderMark,
  ImportExchangeRatesPage,
  ratesCsv,
} from "../../../pages/platform/reference-data/import-exchange-rates.page";

const template = `${byteOrderMark}date,currency,unit,rate,source\r\n31-03-2026,USD,1,85.0000,Example: replace this row with your own rates\r\n`;

const notCsv =
  "rates.xlsx is not a CSV file. In Excel, save the sheet as CSV UTF-8 (Comma delimited), then choose that file.";

const notUtf8 =
  "rates.csv is not saved as UTF-8 text. In Excel, save it as CSV UTF-8 (Comma delimited), then choose it again.";

// The test that imports records rates of HKD and NZD, currencies no other test uses, on days of its own window (rateDays).
test.describe("importing exchange rates", () => {
  test.use({ persona: "administrator" });

  forEachTheme(
    "refuses a workbook and a file not saved as UTF-8, and says so again on every attempt",
    async ({ page, capture }) => {
      const importPage = new ImportExchangeRatesPage(page);
      const workbook = {
        name: "rates.xlsx",
        mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        buffer: Buffer.from([0x50, 0x4b, 0x03, 0x04]),
      };
      const windows1252 = {
        name: "rates.csv",
        mimeType: "text/csv",
        buffer: Buffer.from(
          "date,currency,unit,rate,source\r\n01-01-1901,CHF,1,95,Zürich desk\r\n",
          "latin1",
        ),
      };
      await importPage.goto();
      await importPage.waitUntilInteractive();

      await importPage.chooseThroughButton(workbook);
      await expect(importPage.refusal).toHaveText(notCsv);
      await expect(importPage.status).toHaveText("");
      const firstRefusal = await importPage.refusal.elementHandle();
      await importPage.chooseThroughButton(workbook);
      await expect
        .poll(() => firstRefusal.evaluate((element) => element.isConnected), {
          message: "the second refusal is a new alert",
        })
        .toBe(false);
      await firstRefusal.dispose();
      await expect(importPage.refusal).toHaveText(notCsv);

      await importPage.chooseThroughButton(windows1252);
      await expect(importPage.refusal).toHaveText(notUtf8);
      await expect(importPage.file).toHaveCount(0);
      await expect(importPage.importButton).toHaveCount(0);
      await capture("import-exchange-rates-refusal", importPage.chooser);
    },
  );

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("checks a file by its lines, imports a good one, leaves it as it was the second time and names the API's refusals by line", async ({
        page,
        api,
        capture,
      }, testInfo) => {
        test.slow();
        const importPage = new ImportExchangeRatesPage(page);
        const listPage = new ExchangeRatesPage(page);
        const ratePage = new ExchangeRatePage(page);
        const days = rateDays(testInfo, theme, 3);
        const [first = "", second = "", third = ""] = days;
        await removeRates(api, "HKD", days);
        await removeRates(api, "NZD", days);
        try {
          await importPage.goto();
          await importPage.waitUntilInteractive();
          await expect(page).toHaveTitle("Import rates · ERP-AI-Pro");
          await expect(importPage.columns).toMatchAriaSnapshot(`
            - term: date
            - definition: ${JSON.stringify("The day of the rate, written as 31-03-2026 or 2026-03-31: today or an earlier day.")}
            - term: currency
            - definition: The code of a foreign currency, such as USD.
            - term: unit
            - definition: ${JSON.stringify("How many units of the currency the rate is for: 1, 10, 100, 1,000, 10,000 or 1,00,000.")}
            - term: rate
            - definition: The rupees for those units, with up to 4 digits after the decimal point.
            - term: source
            - definition: Optional. Where the rate comes from, up to 200 characters.
          `);
          await expect(importPage.importButton).toHaveCount(0);
          await capture("import-exchange-rates");

          const saved = await importPage.downloadTemplate();
          expect(saved.name).toBe("exchange-rates-template.csv");
          expect(saved.content.toString("utf8")).toBe(template);

          await importPage.choose(
            ratesCsv("problems.csv", [
              "date,currency,unit,rate",
              `${typedDate(first)},HKD,1,`,
              "01-01-2999,HKD,1,10",
              `${typedDate(second)},XYZ,1,10`,
              `${typedDate(first)},NZD,1,48`,
            ]),
          );
          await expect(importPage.status).toHaveText(
            "problems.csv has 3 problems, so it cannot be imported yet. Correct them in the file, then choose it again.",
          );
          await expect(importPage.problems.getByRole("listitem")).toHaveText([
            "Line 2: the rate is blank.",
            "Line 3: the date 01-01-2999 is after today.",
            "Line 4: the currency XYZ is not one the ERP knows.",
          ]);
          await expect(importPage.rows.getByRole("row")).toHaveCount(2);
          await expect(importPage.importButton).toHaveCount(0);
          await capture("import-exchange-rates-problems", importPage.file);

          const good = ratesCsv("rates.csv", [
            `${byteOrderMark}Currency,Date,Rate,Unit,Source`,
            `HKD,${typedDate(first)},10.5,1,"FBIL reference rate, 1:30 PM"`,
            `hkd,${second},10.75,1,`,
            `NZD,${typedDate(first).replaceAll("-", "/")},48.1234,"100",Test desk`,
          ]);
          await importPage.choose(good);
          await expect(importPage.status).toHaveText("rates.csv holds 3 rates, ready to import.");
          await expect(importPage.problems).toHaveCount(0);
          await expect(importPage.rows.getByRole("table")).toMatchAriaSnapshot(`
            - table "Rates in the file":
              - rowgroup:
                - row:
                  - columnheader "Line"
                  - columnheader "Day"
                  - columnheader "Currency"
                  - columnheader "Rate"
                  - columnheader "Per"
                  - columnheader "Source"
              - rowgroup:
                - row:
                  - cell "2"
                  - cell "${typedDate(first)}"
                  - cell "HKD"
                  - cell "₹10.5000"
                  - cell "1"
                  - cell "FBIL reference rate, 1:30 PM"
                - row:
                  - cell "3"
                  - cell "${typedDate(second)}"
                  - cell "HKD"
                  - cell "₹10.7500"
                  - cell "1"
                  - cell "Not given"
                - row:
                  - cell "4"
                  - cell "${typedDate(first)}"
                  - cell "NZD"
                  - cell "₹48.1234"
                  - cell "100"
                  - cell "Test desk"
          `);
          await expect(importPage.importButton).toHaveText("Import 3 rates");
          await importPage.importButton.click();
          await expect(importPage.outcome).toBeFocused();
          await expect(importPage.outcome).toContainText("Recorded 3 rates.");
          await expect(importPage.outcomeLink).toHaveAttribute(
            "href",
            `${exchangeRatesPath}${ratesOf(["HKD", "NZD"], first, second)}`,
          );
          await capture("import-exchange-rates-recorded", importPage.file);

          await importPage.choose(good);
          await expect(importPage.outcome).toHaveCount(0);
          await importPage.importButton.click();
          await expect(importPage.outcome).toBeFocused();
          await expect(importPage.outcome).toContainText(
            "Recorded no new rates. 3 rates were already recorded and were left as they were.",
          );

          await importPage.outcomeLink.click();
          await expect(page).toHaveURL(
            (url) =>
              url.pathname === exchangeRatesPath && url.search === ratesOf(["HKD", "NZD"], first, second),
          );
          await expect(listPage.list.status).toHaveText(
            "Showing all 3 exchange rates. Sorted by Day, latest first.",
          );
          await expect
            .poll(() => listPage.list.titles())
            .toEqual([typedDate(second), typedDate(first), typedDate(first)]);
          const importedNzd = `NZD on ${typedDate(first)}`;
          await listPage.rateLink(importedNzd).click();
          await expect(ratePage.heading).toHaveText(importedNzd);
          await expect(ratePage.details).toContainText("₹48.1234 for 100 NZD");

          await importPage.goto();
          await importPage.waitUntilInteractive();
          await importPage.choose(
            ratesCsv("changed.csv", [
              "date,currency,unit,rate",
              `${typedDate(third)},HKD,1,11`,
              `${typedDate(first)},HKD,1,12`,
            ]),
          );
          await expect(importPage.status).toHaveText("changed.csv holds 2 rates, ready to import.");
          await importPage.importButton.click();
          await expect(importPage.summary).toBeFocused();
          await expect(importPage.summary.getByRole("listitem")).toHaveText([
            "No rate of the file was recorded. Correct these lines in the file, then choose it again.",
            new RegExp(
              `^Line 3: HKD for ${typedDate(first)} is already recorded as 10\\.50* rupees for 1; correct that rate instead\\.$`,
            ),
          ]);
          await expect(importPage.importButton).toBeVisible();
          await capture("import-exchange-rates-refused", importPage.file);

          const refusedDay = new URLSearchParams({ filter: `currency:eq:HKD;rateDate:eq:${third}` });
          const recordedOnRefusedDay = await api.get(`${exchangeRatesApi}?${refusedDay.toString()}`);
          expect(recordedOnRefusedDay.status()).toBe(200);
          expect(
            ((await recordedOnRefusedDay.json()) as { totalCount?: number }).totalCount,
            "the rates of the refused file that were recorded",
          ).toBe(0);
        } finally {
          await removeRates(api, "HKD", days);
          await removeRates(api, "NZD", days);
        }
      });
    });
  }
});
