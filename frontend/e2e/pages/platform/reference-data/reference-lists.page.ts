import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";

export const countriesPath = "/platform/reference-data";

export const indianStatesPath = "/platform/reference-data/indian-states";

export const currenciesPath = "/platform/reference-data/currencies";

export const financialYearsPath = "/platform/reference-data/financial-years";

export const referenceListsApi = {
  countries: "/api/platform/reference-data/countries",
  indianStates: "/api/platform/reference-data/indian-states",
  currencies: "/api/platform/reference-data/currencies",
  financialYears: "/api/platform/reference-data/financial-years",
} as const;

export const referenceListTabs = [
  { name: "Countries", path: countriesPath },
  { name: "States and union territories", path: indianStatesPath },
  { name: "Currencies", path: currenciesPath },
  { name: "Financial years", path: financialYearsPath },
] as const;

export type ReferenceListTab = (typeof referenceListTabs)[number]["name"];

export type CountryEntry = { readonly code: string; readonly name: string };

export type IndianStateEntry = {
  readonly gstCode: string;
  readonly name: string;
  readonly kind: "state" | "unionTerritory" | "otherTerritory";
  readonly validFrom: string;
  readonly validUntil: string | null;
};

export type CurrencyEntry = { readonly code: string; readonly name: string; readonly minorUnits: number };

export type FinancialQuarterEntry = {
  readonly number: number;
  readonly label: string;
  readonly start: string;
  readonly end: string;
};

export type FinancialPeriodEntry = {
  readonly number: number;
  readonly label: string;
  readonly quarter: number;
  readonly start: string;
  readonly end: string;
};

export type FinancialYearEntry = {
  readonly label: string;
  readonly start: string;
  readonly end: string;
  readonly quarters: readonly FinancialQuarterEntry[];
  readonly periods: readonly FinancialPeriodEntry[];
};

export type ShownRow = Readonly<Record<string, string>>;

export type ReferenceList = {
  readonly path: string;
  readonly title: ReferenceListTab;
  readonly card: string;
  readonly listHeading: string;
};

export const countriesList: ReferenceList = {
  path: countriesPath,
  title: "Countries",
  card: "countries-card",
  listHeading: "All countries",
};

export const indianStatesList: ReferenceList = {
  path: indianStatesPath,
  title: "States and union territories",
  card: "indian-states-card",
  listHeading: "GST State codes",
};

export const currenciesList: ReferenceList = {
  path: currenciesPath,
  title: "Currencies",
  card: "currencies-card",
  listHeading: "All currencies",
};

const territoryKinds: Readonly<Record<IndianStateEntry["kind"], string>> = {
  state: "State",
  unionTerritory: "Union territory",
  otherTerritory: "Other territory",
};

const counts = new Intl.NumberFormat("en-IN");

export function count(value: number): string {
  return counts.format(value);
}

export function referenceListAddress(
  path: string,
  { q, page }: { readonly q?: string; readonly page?: number } = {},
): string {
  const search = new URLSearchParams();
  if (q !== undefined) search.set("q", q);
  if (page !== undefined && page > 1) search.set("page", String(page));
  const query = search.toString();
  return query === "" ? path : `${path}?${query}`;
}

export function dayMonthYear(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  return `${day}-${month}-${year}`;
}

export function dayAfter(isoDate: string): string {
  const day = new Date(`${isoDate}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() + 1);
  return day.toISOString().slice(0, 10);
}

export function todayInIndia(): string {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Kolkata",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((candidate) => candidate.type === type)?.value;
  return `${part("year")}-${part("month")}-${part("day")}`;
}

// The API names a financial year by the calendar year it starts in and the last two digits of the next.
export function financialYearLabel(startYear: number): string {
  return `${startYear}-${String((startYear + 1) % 100).padStart(2, "0")}`;
}

async function readList<T>(request: APIRequestContext, path: string, member: string): Promise<T[]> {
  const response = await request.get(path);
  expect(response.status(), path).toBe(200);
  const body = (await response.json()) as Record<string, readonly T[] | undefined>;
  return [...(body[member] ?? [])];
}

export function readCountries(request: APIRequestContext): Promise<CountryEntry[]> {
  return readList(request, referenceListsApi.countries, "countries");
}

export function readIndianStates(request: APIRequestContext): Promise<IndianStateEntry[]> {
  return readList(request, referenceListsApi.indianStates, "states");
}

export function readCurrencies(request: APIRequestContext): Promise<CurrencyEntry[]> {
  return readList(request, referenceListsApi.currencies, "currencies");
}

export function readFinancialYears(
  request: APIRequestContext,
  from: string,
  to: string,
): Promise<FinancialYearEntry[]> {
  return readList(request, `${referenceListsApi.financialYears}?from=${from}&to=${to}`, "years");
}

// The current year is the one the API says holds today in India, so no test decides where a financial year starts.
export async function currentAndPreviousFinancialYears(
  request: APIRequestContext,
): Promise<{ readonly current: FinancialYearEntry; readonly previous: FinancialYearEntry }> {
  const today = todayInIndia();
  const calendarYear = Number(today.slice(0, 4));
  const years = await readFinancialYears(
    request,
    financialYearLabel(calendarYear - 2),
    financialYearLabel(calendarYear),
  );
  const index = years.findIndex((year) => year.start <= today && today <= year.end);
  const current = years[index];
  const previous = years[index - 1];
  expect(current, `the financial year that holds ${today}`).toBeDefined();
  expect(previous, `the financial year before the one that holds ${today}`).toBeDefined();
  return { current: current as FinancialYearEntry, previous: previous as FinancialYearEntry };
}

function comparable(text: string): string {
  return text.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase().trim();
}

// What a person expects a search to find, written apart from the page's own code: every typed word inside the code or the
// name, ignoring case and accents, in the list's order, except that a code typed in full comes first.
export function expectedMatches<T>(
  items: readonly T[],
  search: string,
  keysOf: (item: T) => { readonly code: string; readonly name: string },
): T[] {
  const typed = comparable(search);
  const words = typed.split(/\s+/u);
  const holds = (item: T) => {
    const { code, name } = keysOf(item);
    return words.every((word) => comparable(code).includes(word) || comparable(name).includes(word));
  };
  const fullCode = (item: T) => comparable(keysOf(item).code) === typed;
  return [...items.filter(fullCode), ...items.filter((item) => !fullCode(item) && holds(item))];
}

export function countryRow(country: CountryEntry): ShownRow {
  return { Code: country.code, Name: country.name };
}

export function indianStateKeys(state: IndianStateEntry): { readonly code: string; readonly name: string } {
  return { code: state.gstCode, name: state.name };
}

export function indianStateRow(state: IndianStateEntry): ShownRow {
  return {
    "GST State code": state.gstCode,
    Name: state.name,
    Type: territoryKinds[state.kind],
    From: dayMonthYear(state.validFrom),
    Until: state.validUntil === null ? "In force" : dayMonthYear(state.validUntil),
  };
}

export function currencyRow(currency: CurrencyEntry): ShownRow {
  return { Code: currency.code, Name: currency.name, "Decimal places": String(currency.minorUnits) };
}

export function quarterRow(quarter: FinancialQuarterEntry): ShownRow {
  return { Quarter: quarter.label, From: dayMonthYear(quarter.start), Until: dayMonthYear(quarter.end) };
}

export function periodRows(year: FinancialYearEntry): ShownRow[] {
  return year.periods.map((period) => ({
    Month: period.label,
    Period: String(period.number),
    Quarter:
      year.quarters.find((quarter) => quarter.number === period.quarter)?.label ?? String(period.quarter),
    From: dayMonthYear(period.start),
    Until: dayMonthYear(period.end),
  }));
}

// A list draws its rows twice, as a table and as cards, and shows one of them by the width of its box; each row is read
// from whichever is shown, as its values by column heading.
export async function shownRows(container: Locator, titleHeading: string): Promise<ShownRow[]> {
  const table = container.getByTestId("reference-list-table");
  const cardList = container.getByTestId("reference-list-cards");
  await expect
    .poll(async () => (await table.isVisible()) || (await cardList.isVisible()), {
      message: "the rows show as a table or as cards",
    })
    .toBe(true);
  if (await table.isVisible()) {
    const headings = await table.locator("thead th").allInnerTexts();
    return table
      .getByTestId("reference-list-row")
      .evaluateAll(
        (rows, columns) =>
          rows.map((row) =>
            Object.fromEntries(
              Array.from(row.querySelectorAll("th, td"), (cell, index) => [
                columns[index] ?? String(index),
                (cell as HTMLElement).innerText.trim(),
              ]),
            ),
          ),
        headings,
      );
  }
  return cardList.getByTestId("reference-list-card").evaluateAll(
    (cards, title) =>
      cards.map((card) => {
        const titleText = card.querySelector("[data-testid='reference-list-card-title']");
        const terms = Array.from(card.querySelectorAll("dt"), (term) =>
          (term as HTMLElement).innerText.trim(),
        );
        const details = Array.from(card.querySelectorAll("dd"), (detail) =>
          (detail as HTMLElement).innerText.trim(),
        );
        return Object.fromEntries([
          [title, titleText instanceof HTMLElement ? titleText.innerText.trim() : ""],
          ...terms.map((term, index) => [term, details[index] ?? ""]),
        ]);
      }),
    titleHeading,
  );
}

export class ReferenceListsShell {
  readonly heading: Locator;
  readonly tabs: Locator;

  constructor(protected readonly page: Page) {
    this.heading = page.getByRole("main").getByRole("heading", { level: 1 });
    this.tabs = page.getByRole("navigation", { name: "Reference lists" });
  }

  tab(name: ReferenceListTab): Locator {
    return this.tabs.getByRole("link", { name, exact: true });
  }

  // The search and the year's chooser work through React, so they mark themselves as rendering the server's state until the
  // page hydrates.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }
}

export class ReferenceListPage extends ReferenceListsShell {
  readonly card: Locator;
  readonly listHeading: Locator;
  readonly list: Locator;
  readonly search: Locator;
  readonly searchBox: Locator;
  readonly searchButton: Locator;
  readonly clearSearch: Locator;
  readonly status: Locator;
  readonly pages: Locator;
  readonly pageLabel: Locator;
  readonly noMatches: Locator;
  readonly unavailable: Locator;

  constructor(
    page: Page,
    readonly definition: ReferenceList,
  ) {
    super(page);
    this.card = page.getByTestId(definition.card);
    this.listHeading = this.card.getByRole("heading", { level: 2 });
    this.list = this.card.getByTestId("reference-list");
    this.search = this.card.getByRole("search", { name: `Search ${definition.title.toLowerCase()}` });
    this.searchBox = this.search.getByRole("searchbox", { name: "Name or code" });
    this.searchButton = this.search.getByRole("button", { name: "Search", exact: true });
    this.clearSearch = this.search.getByRole("link", { name: "Clear the search", exact: true });
    this.status = this.card.getByTestId("data-table-status");
    this.pages = this.card.getByRole("navigation", { name: `${definition.title}, pages` });
    this.pageLabel = this.card.getByTestId("reference-list-page");
    this.noMatches = this.card.getByTestId("reference-list-no-matches");
    this.unavailable = this.card.getByTestId("reference-list-unavailable");
  }

  async goto(address = this.definition.path): Promise<void> {
    await this.page.goto(address);
    await expect(this.heading).toHaveText(this.definition.title);
  }

  pageLink(name: "First page" | "Previous page" | "Next page" | "Last page"): Locator {
    return this.pages.getByRole("link", { name, exact: true });
  }

  rows(): Promise<ShownRow[]> {
    return shownRows(this.list, "Name");
  }

  async names(): Promise<string[]> {
    return (await this.rows()).map((row) => row["Name"] ?? "");
  }

  async searchFor(text: string): Promise<void> {
    await this.searchBox.fill(text);
    await this.searchBox.press("Enter");
  }

  // The rows are a table on a wide screen and cards on a narrow one.
  async showsCards(): Promise<boolean> {
    await shownRows(this.list, "Name");
    return this.list.getByTestId("reference-list-cards").isVisible();
  }

  row(name: string): Locator {
    return this.list
      .getByTestId("reference-list-row")
      .or(this.list.getByTestId("reference-list-card"))
      .filter({ visible: true })
      .filter({ hasText: name });
  }
}

export class FinancialYearsPage extends ReferenceListsShell {
  readonly card: Locator;
  readonly yearHeading: Locator;
  readonly yearDays: Locator;
  readonly chooser: Locator;
  readonly picker: Locator;
  readonly show: Locator;
  readonly status: Locator;
  readonly quarters: Locator;
  readonly periods: Locator;
  readonly unavailable: Locator;

  constructor(page: Page) {
    super(page);
    this.card = page.getByTestId("financial-years-card");
    this.yearHeading = this.card.getByRole("heading", { level: 2 });
    this.yearDays = this.card.locator("[data-slot='card-description']");
    this.chooser = page.getByRole("form", { name: "Choose a financial year" });
    this.picker = this.chooser.getByRole("combobox", { name: "Financial year" });
    this.show = this.chooser.getByRole("button", { name: "Show", exact: true });
    this.status = page.getByTestId("financial-year-status");
    this.quarters = page.getByTestId("financial-year-quarters");
    this.periods = page.getByTestId("financial-year-periods");
    this.unavailable = this.card.getByTestId("reference-list-unavailable");
  }

  async goto(address = financialYearsPath): Promise<void> {
    await this.page.goto(address);
    await expect(this.heading).toHaveText("Financial years");
  }

  quarterRows(): Promise<ShownRow[]> {
    return shownRows(this.quarters, "Quarter");
  }

  periodRows(): Promise<ShownRow[]> {
    return shownRows(this.periods, "Month");
  }

  // The year is typed, picked with the arrow keys and Enter, and shown with Enter on Show, as a keyboard user does.
  async chooseYear(label: string): Promise<void> {
    await this.picker.fill(label);
    const list = this.page.getByRole("listbox", { name: "Financial year", exact: true });
    await expect(list.getByRole("option")).toHaveCount(1);
    await this.picker.press("ArrowDown");
    await expect(list.getByRole("option").filter({ hasText: label })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    await this.picker.press("Enter");
    await expect(list).toBeHidden();
    await expect(this.picker).toHaveValue(label);
    await this.show.focus();
    await this.page.keyboard.press("Enter");
  }
}
