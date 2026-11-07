import { expect, forEachTheme, tabOntoLink, test } from "../../../fixtures/test";
import {
  count,
  countriesList,
  countriesPath,
  countryRow,
  currenciesList,
  currenciesPath,
  currencyRow,
  currentAndPreviousFinancialYears,
  dayMonthYear,
  expectedMatches,
  FinancialYearsPage,
  financialYearsPath,
  indianStateKeys,
  indianStateRow,
  indianStatesList,
  indianStatesPath,
  periodRows,
  quarterRow,
  readCountries,
  readCurrencies,
  readIndianStates,
  referenceListAddress,
  ReferenceListPage,
  referenceListTabs,
} from "../../../pages/platform/reference-data/reference-lists.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const countriesPerPage = 20;

const currenciesPerPage = 20;

const codesPerPage = 10;

const quoted = JSON.stringify;

function shownCount(total: number, perPage: number, noun: { one: string; other: string }): string {
  if (total === 1) return `Showing 1 ${noun.one}.`;
  if (total <= perPage) return `Showing all ${count(total)} ${noun.other}.`;
  return `Showing 1–${count(perPage)} of ${count(total)} ${noun.other}.`;
}

// The accountant holds the Employee permissions alone, and every employee reads the reference lists, so each test here is
// what any employee meets.
test.describe("reference lists", () => {
  forEachTheme(
    "open from the menu on the countries, found by name and by code",
    async ({ page, capture }) => {
      const countries = await readCountries(page.request);
      const india = countries.find((country) => country.code === "IN");
      expect(india, "the country IN").toBeDefined();
      const shell = new AppShell(page);
      const list = new ReferenceListPage(page, countriesList);

      await page.goto("/");
      await shell.openNavigation();
      await expect(shell.navigationLink("Reference lists")).toBeVisible();
      await shell.navigationLink("Reference lists").click();
      await expect(page).toHaveURL((url) => url.pathname === countriesPath && url.search === "");
      await expect(list.heading).toHaveText("Countries");
      await expect(page).toHaveTitle("Countries · ERP-AI-Pro");
      await list.waitUntilInteractive();

      await expect(list.tabs).toMatchAriaSnapshot(`
        - navigation "Reference lists":
          - list:
            - listitem:
              - link "Countries":
                - /url: ${countriesPath}
            - listitem:
              - link "States and union territories":
                - /url: ${indianStatesPath}
            - listitem:
              - link "Currencies":
                - /url: ${currenciesPath}
            - listitem:
              - link "Financial years":
                - /url: ${financialYearsPath}
      `);
      await expect(list.tab("Countries")).toHaveAttribute("aria-current", "page");
      for (const tab of referenceListTabs.slice(1)) {
        await expect(list.tab(tab.name), tab.name).not.toHaveAttribute("aria-current");
      }
      await expect(list.listHeading).toHaveText("All countries");
      await expect(list.search).toMatchAriaSnapshot(`
        - search "Search countries":
          - group:
            - text: Name or code
            - group:
              - group
              - searchbox "Name or code":
                - /placeholder: India or IN
          - button "Search"
      `);
      await expect(list.status).toHaveText(
        shownCount(countries.length, countriesPerPage, { one: "country", other: "countries" }),
      );
      await expect(list.pageLabel).toHaveText(`Page 1 of ${Math.ceil(countries.length / countriesPerPage)}`);
      expect(await list.rows()).toEqual(countries.slice(0, countriesPerPage).map(countryRow));
      await capture("countries");

      const united = expectedMatches(countries, "united", (country) => country);
      expect(united.length, "countries named United").toBeGreaterThan(1);
      await list.searchFor("united");
      await expect(page).toHaveURL((url) => url.searchParams.get("q") === "united");
      await expect(list.status).toHaveText(
        shownCount(united.length, countriesPerPage, { one: "country", other: "countries" }),
      );
      await expect.poll(() => list.rows()).toEqual(united.slice(0, countriesPerPage).map(countryRow));
      await expect(list.searchBox).toBeFocused();

      const byCode = expectedMatches(countries, "IN", (country) => country);
      await list.searchFor("IN");
      await expect(page).toHaveURL((url) => url.searchParams.get("q") === "IN");
      await expect.poll(() => list.rows()).toEqual(byCode.slice(0, countriesPerPage).map(countryRow));
      expect((await list.rows())[0], "the country whose code was typed in full").toEqual(countryRow(india!));
      await capture("countries-found-by-code", list.card);

      await list.clearSearch.click();
      await expect(page).toHaveURL((url) => url.pathname === countriesPath && url.search === "");
      await expect(list.searchBox).toHaveValue("");
      await expect(list.searchBox).toBeFocused();
      await expect(list.clearSearch).toHaveCount(0);

      await list.searchFor("atlantis");
      await expect(list.noMatches).toContainText("No countries match this search.");
      await expect(list.status).toHaveText("No countries to show.");
      await expect(list.pages).toHaveCount(0);
      await capture("countries-no-matches", list.card);
      await list.noMatches.getByRole("link", { name: "Clear the search", exact: true }).click();
      await expect(page).toHaveURL((url) => url.search === "");
      await expect(list.searchBox).toBeFocused();
    },
  );

  test("pages through the countries and keeps the address canonical", async ({ page }) => {
    const countries = await readCountries(page.request);
    const pageCount = Math.ceil(countries.length / countriesPerPage);
    const names = (from: number, to?: number) => countries.slice(from, to).map((country) => country.name);
    const list = new ReferenceListPage(page, countriesList);
    await list.goto();
    await list.waitUntilInteractive();

    await expect(list.pageLink("First page")).toHaveAttribute("aria-disabled", "true");
    await expect(list.pageLink("Previous page")).toHaveAttribute("aria-disabled", "true");
    await list.pageLink("Next page").focus();
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
    await expect(list.pageLabel).toHaveText(`Page 2 of ${pageCount}`);
    await expect.poll(() => list.names()).toEqual(names(countriesPerPage, 2 * countriesPerPage));
    await expect(list.pageLink("Next page")).toBeFocused();

    await list.pageLink("Last page").click();
    await expect(page).toHaveURL((url) => url.searchParams.get("page") === String(pageCount));
    await expect.poll(() => list.names()).toEqual(names((pageCount - 1) * countriesPerPage));
    await expect(list.pageLink("Next page")).toHaveAttribute("aria-disabled", "true");
    await expect(list.pageLink("Last page")).toHaveAttribute("aria-disabled", "true");

    await list.pageLink("First page").click();
    await expect(page).toHaveURL((url) => url.pathname === countriesPath && url.search === "");
    await expect.poll(() => list.names()).toEqual(names(0, countriesPerPage));

    for (const { address, canonical } of [
      { address: referenceListAddress(countriesPath, { page: 99 }), canonical: `?page=${pageCount}` },
      { address: `${countriesPath}?page=1`, canonical: "" },
      { address: `${countriesPath}?page=two`, canonical: "" },
      { address: `${countriesPath}?q=%20india%20`, canonical: "?q=india" },
      { address: `${countriesPath}?q=&page=2`, canonical: "?page=2" },
      { address: `${countriesPath}?sort=name`, canonical: "" },
    ]) {
      await page.goto(address);
      await expect(page, address).toHaveURL(
        (url) => url.pathname === countriesPath && url.search === canonical,
      );
      await expect(list.heading).toHaveText("Countries");
    }
  });

  test("moves between the lists with the tabs from the keyboard", async ({ page }) => {
    const list = new ReferenceListPage(page, countriesList);
    await list.goto();

    for (const tab of referenceListTabs.slice(1)) {
      await tabOntoLink(page, list.tab(tab.name));
      await page.keyboard.press("Enter");
      await expect(page).toHaveURL((url) => url.pathname === tab.path);
      await expect(list.heading).toHaveText(tab.name);
      await expect(page).toHaveTitle(`${tab.name} · ERP-AI-Pro`);
      await expect(list.tab(tab.name)).toHaveAttribute("aria-current", "page");
      await expect(list.tab("Countries")).not.toHaveAttribute("aria-current");
    }
  });

  forEachTheme(
    "shows every GST State code with its days, the ended code 25 among them",
    async ({ page, capture }) => {
      const states = await readIndianStates(page.request);
      const ended = states.find((state) => state.gstCode === "25" && state.validUntil !== null);
      expect(ended, "the GST State code 25, which no longer applies").toBeDefined();
      const list = new ReferenceListPage(page, indianStatesList);
      await page.goto(countriesPath);
      await list.tab("States and union territories").click();
      await expect(page).toHaveURL((url) => url.pathname === indianStatesPath);
      await expect(list.heading).toHaveText("States and union territories");
      await expect(page).toHaveTitle("States and union territories · ERP-AI-Pro");
      await list.waitUntilInteractive();

      await expect(list.tab("States and union territories")).toHaveAttribute("aria-current", "page");
      await expect(list.listHeading).toHaveText("GST State codes");
      await expect(list.status).toHaveText(
        shownCount(states.length, codesPerPage, { one: "entry", other: "entries" }),
      );
      await expect(list.pageLabel).toHaveText(`Page 1 of ${Math.ceil(states.length / codesPerPage)}`);
      expect(await list.rows()).toEqual(states.slice(0, codesPerPage).map(indianStateRow));
      await capture("indian-states");

      await list.searchFor("25");
      await expect(page).toHaveURL((url) => url.searchParams.get("q") === "25");
      const byCode = expectedMatches(states, "25", indianStateKeys);
      await expect.poll(() => list.rows()).toEqual(byCode.map(indianStateRow));
      await expect(list.status).toHaveText(
        shownCount(byCode.length, codesPerPage, { one: "entry", other: "entries" }),
      );
      const shown = indianStateRow(ended!);
      if (await list.showsCards()) {
        await expect(list.row(ended!.name)).toMatchAriaSnapshot(`
          - listitem:
            - paragraph: ${quoted(shown["Name"])}
            - term: GST State code
            - definition: ${quoted(shown["GST State code"])}
            - term: Type
            - definition: ${quoted(shown["Type"])}
            - term: From
            - definition: ${quoted(shown["From"])}
            - term: Until
            - definition: ${quoted(shown["Until"])}
        `);
      } else {
        await expect(list.row(ended!.name)).toMatchAriaSnapshot(`
          - row:
            - cell ${quoted(shown["GST State code"])}
            - rowheader ${quoted(shown["Name"])}
            - cell ${quoted(shown["Type"])}
            - cell ${quoted(shown["From"])}
            - cell ${quoted(shown["Until"])}
        `);
      }
      await capture("indian-states-ended-code", list.card);

      const byName = expectedMatches(states, "daman", indianStateKeys);
      expect(byName.length, "entries named Daman").toBeGreaterThan(1);
      await list.searchFor("daman");
      await expect.poll(() => list.rows()).toEqual(byName.map(indianStateRow));
    },
  );

  test("pages through every GST State code entry", async ({ page }) => {
    const states = await readIndianStates(page.request);
    const pageCount = Math.ceil(states.length / codesPerPage);
    const list = new ReferenceListPage(page, indianStatesList);
    await list.goto(referenceListAddress(indianStatesPath, { page: pageCount }));
    await list.waitUntilInteractive();

    await expect(list.pageLabel).toHaveText(`Page ${pageCount} of ${pageCount}`);
    expect(await list.rows()).toEqual(states.slice((pageCount - 1) * codesPerPage).map(indianStateRow));
    await list.pageLink("Previous page").click();
    await expect(page).toHaveURL((url) => url.searchParams.get("page") === String(pageCount - 1));
    await expect
      .poll(() => list.rows())
      .toEqual(
        states.slice((pageCount - 2) * codesPerPage, (pageCount - 1) * codesPerPage).map(indianStateRow),
      );
  });

  forEachTheme(
    "shows every currency with its decimal places, found by its code",
    async ({ page, capture }) => {
      const currencies = await readCurrencies(page.request);
      const dollar = currencies.find((currency) => currency.code === "USD");
      expect(dollar, "the currency USD").toBeDefined();
      const list = new ReferenceListPage(page, currenciesList);
      await page.goto(countriesPath);
      await list.tab("Currencies").click();
      await expect(page).toHaveURL((url) => url.pathname === currenciesPath);
      await expect(list.heading).toHaveText("Currencies");
      await list.waitUntilInteractive();

      await expect(list.tab("Currencies")).toHaveAttribute("aria-current", "page");
      await expect(list.status).toHaveText(
        shownCount(currencies.length, currenciesPerPage, { one: "currency", other: "currencies" }),
      );
      expect(await list.rows()).toEqual(currencies.slice(0, currenciesPerPage).map(currencyRow));
      await capture("currencies");

      await list.searchFor("usd");
      await expect(page).toHaveURL((url) => url.searchParams.get("q") === "usd");
      const byCode = expectedMatches(currencies, "usd", (currency) => currency);
      await expect.poll(() => list.rows()).toEqual(byCode.slice(0, currenciesPerPage).map(currencyRow));
      expect((await list.rows())[0], "the currency whose code was typed in full").toEqual(
        currencyRow(dollar!),
      );
      await capture("currencies-found-by-code", list.card);
    },
  );

  forEachTheme(
    "shows the quarters and monthly periods of the chosen financial year",
    async ({ page, capture }) => {
      const { current, previous } = await currentAndPreviousFinancialYears(page.request);
      const years = new FinancialYearsPage(page);
      await page.goto(countriesPath);
      await years.tab("Financial years").click();
      await expect(page).toHaveURL((url) => url.pathname === financialYearsPath && url.search === "");
      await expect(years.heading).toHaveText("Financial years");
      await expect(page).toHaveTitle("Financial years · ERP-AI-Pro");
      await years.waitUntilInteractive();

      await expect(years.tab("Financial years")).toHaveAttribute("aria-current", "page");
      await expect(years.yearHeading).toHaveText(`Financial year ${current.label}`);
      await expect(years.yearDays).toHaveText(
        `From ${dayMonthYear(current.start)} to ${dayMonthYear(current.end)}.`,
      );
      await expect(years.picker).toHaveValue(current.label);
      await expect(years.status).toHaveText(`Showing financial year ${current.label}.`);
      await expect(years.chooser).toMatchAriaSnapshot(`
        - form "Choose a financial year":
          - group:
            - text: Financial year
            - group:
              - combobox "Financial year"
          - button "Show"
      `);
      expect(await years.quarterRows()).toEqual(current.quarters.map(quarterRow));
      expect(await years.periodRows()).toEqual(periodRows(current));
      await capture("financial-years");

      await years.chooseYear(previous.label);
      await expect(page).toHaveURL((url) => url.searchParams.get("year") === previous.label);
      await expect(years.yearHeading).toHaveText(`Financial year ${previous.label}`);
      await expect(years.status).toHaveText(`Showing financial year ${previous.label}.`);
      await expect.poll(() => years.periodRows()).toEqual(periodRows(previous));
      expect(await years.quarterRows()).toEqual(previous.quarters.map(quarterRow));
      await expect(years.show).toBeFocused();
      await expect(years.picker).toHaveValue(previous.label);

      await page.goBack();
      await expect(page).toHaveURL((url) => url.pathname === financialYearsPath && url.search === "");
      await expect(years.yearHeading).toHaveText(`Financial year ${current.label}`);
      await expect(years.picker).toHaveValue(current.label);
    },
  );

  test("asks for a financial year before showing one and keeps the address canonical", async ({ page }) => {
    const { current } = await currentAndPreviousFinancialYears(page.request);
    const years = new FinancialYearsPage(page);
    await years.goto();
    await years.waitUntilInteractive();

    await years.picker.fill("");
    await years.show.click();
    await expect(years.picker).toBeFocused();
    await expect(years.picker).toHaveAttribute("aria-invalid", "true");
    await expect(years.chooser.getByText("Choose a financial year.", { exact: true })).toBeVisible();
    await expect(page).toHaveURL((url) => url.search === "");

    for (const address of [
      `${financialYearsPath}?year=${current.label}`,
      `${financialYearsPath}?year=1999-00`,
      `${financialYearsPath}?year=2026`,
      `${financialYearsPath}?page=2`,
    ]) {
      await page.goto(address);
      await expect(page, address).toHaveURL(
        (url) => url.pathname === financialYearsPath && url.search === "",
      );
      await expect(years.yearHeading).toHaveText(`Financial year ${current.label}`);
    }
  });
});
