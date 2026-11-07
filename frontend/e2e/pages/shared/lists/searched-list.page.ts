import { expect, type Locator } from "@playwright/test";

export type ShownRow = Readonly<Record<string, string>>;

export type SearchedListNames = {
  readonly label: string;
  readonly searchLabel: string;
  readonly titleHeading: string;
};

export function searchedListAddress(
  path: string,
  { q, page }: { readonly q?: string; readonly page?: number } = {},
): string {
  const search = new URLSearchParams();
  if (q !== undefined) search.set("q", q);
  if (page !== undefined && page > 1) search.set("page", String(page));
  const query = search.toString();
  return query === "" ? path : `${path}?${query}`;
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

async function rowsShown(table: Locator, cardList: Locator): Promise<void> {
  await expect
    .poll(async () => (await table.isVisible()) || (await cardList.isVisible()), {
      message: "the rows show as a table or as cards",
    })
    .toBe(true);
}

// A list draws its rows twice, as a table and as cards, and shows one of them by the width of its box; each row is read
// from whichever is shown, as its values by column heading.
export async function shownRows(container: Locator, titleHeading: string): Promise<ShownRow[]> {
  const table = container.getByTestId("searched-list-table");
  const cardList = container.getByTestId("searched-list-cards");
  await rowsShown(table, cardList);
  if (await table.isVisible()) {
    const headings = await table.locator("thead th").allInnerTexts();
    return table
      .getByTestId("searched-list-row")
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
  return cardList.getByTestId("searched-list-card").evaluateAll(
    (cards, title) =>
      cards.map((card) => {
        const titleText = card.querySelector("[data-testid='searched-list-card-title']");
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

export class SearchedListRegion {
  readonly root: Locator;
  readonly search: Locator;
  readonly searchBox: Locator;
  readonly searchButton: Locator;
  readonly clearSearch: Locator;
  readonly status: Locator;
  readonly pages: Locator;
  readonly pageLabel: Locator;
  readonly noMatches: Locator;

  constructor(
    readonly container: Locator,
    private readonly names: SearchedListNames,
  ) {
    this.root = container.getByTestId("searched-list");
    this.search = container.getByRole("search", { name: `Search ${names.label.toLowerCase()}` });
    this.searchBox = this.search.getByRole("searchbox", { name: names.searchLabel });
    this.searchButton = this.search.getByRole("button", { name: "Search", exact: true });
    this.clearSearch = this.search.getByRole("link", { name: "Clear the search", exact: true });
    this.status = container.getByTestId("data-table-status");
    this.pages = container.getByRole("navigation", { name: `${names.label}, pages` });
    this.pageLabel = container.getByTestId("searched-list-page");
    this.noMatches = container.getByTestId("searched-list-no-matches");
  }

  pageLink(name: "First page" | "Previous page" | "Next page" | "Last page"): Locator {
    return this.pages.getByRole("link", { name, exact: true });
  }

  rows(): Promise<ShownRow[]> {
    return shownRows(this.root, this.names.titleHeading);
  }

  async titles(): Promise<string[]> {
    return (await this.rows()).map((row) => row[this.names.titleHeading] ?? "");
  }

  async searchFor(text: string): Promise<void> {
    await this.searchBox.fill(text);
    await this.searchBox.press("Enter");
  }

  // The rows are a table on a wide screen and cards on a narrow one.
  async showsCards(): Promise<boolean> {
    const cardList = this.root.getByTestId("searched-list-cards");
    await rowsShown(this.root.getByTestId("searched-list-table"), cardList);
    return cardList.isVisible();
  }

  row(text: string): Locator {
    return this.root
      .getByTestId("searched-list-row")
      .or(this.root.getByTestId("searched-list-card"))
      .filter({ visible: true })
      .filter({ hasText: text });
  }
}
