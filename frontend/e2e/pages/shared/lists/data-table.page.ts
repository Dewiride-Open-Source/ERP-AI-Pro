import { expect, type Locator, type Page } from "@playwright/test";

export class DataTableRegion {
  readonly root: Locator;
  readonly table: Locator;
  readonly tableRows: Locator;
  readonly cardList: Locator;
  readonly cards: Locator;
  readonly status: Locator;
  readonly pageLabel: Locator;
  readonly pageSize: Locator;
  readonly columnsButton: Locator;
  readonly sortSelect: Locator;
  readonly selection: Locator;
  readonly filters: Locator;
  readonly applyFilters: Locator;
  readonly noMatches: Locator;
  readonly rangeTrigger: Locator;
  readonly firstPage: Locator;
  readonly previousPage: Locator;
  readonly nextPage: Locator;
  readonly lastPage: Locator;

  constructor(
    private readonly page: Page,
    readonly container: Locator,
  ) {
    this.root = container.locator("[data-slot='data-table']");
    this.table = container.getByTestId("data-table");
    this.tableRows = this.table.getByTestId("data-table-row");
    this.cardList = container.getByTestId("data-table-cards");
    this.cards = this.cardList.getByTestId("data-table-card");
    this.status = container.getByTestId("data-table-status");
    this.pageLabel = container.getByTestId("data-table-page");
    this.pageSize = container.getByTestId("data-table-page-size");
    this.columnsButton = container.getByTestId("data-table-columns");
    this.sortSelect = container.getByTestId("data-table-sort");
    this.selection = container.getByTestId("data-table-selection");
    this.filters = container.getByRole("search");
    this.applyFilters = this.filters.getByRole("button", { name: "Apply filters" });
    this.noMatches = container.getByTestId("list-no-matches");
    this.rangeTrigger = this.filters.locator("button[aria-label='Choose dates']");
    this.firstPage = container.getByRole("link", { name: "First page" });
    this.previousPage = container.getByRole("link", { name: "Previous page" });
    this.nextPage = container.getByRole("link", { name: "Next page" });
    this.lastPage = container.getByRole("link", { name: "Last page" });
  }

  // The column menu, like every control that works only through React, is disabled in the server's HTML and enabled once
  // the page hydrates, so it marks the list as live.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.columnsButton).toBeEnabled();
  }

  async showsCards(): Promise<boolean> {
    await expect
      .poll(async () => (await this.table.isVisible()) || (await this.cardList.isVisible()), {
        message: "the rows show as a table or as cards",
      })
      .toBe(true);
    return this.cardList.isVisible();
  }

  async rows(): Promise<Locator> {
    return (await this.showsCards()) ? this.cards : this.tableRows;
  }

  async row(text: string): Promise<Locator> {
    return (await this.rows()).filter({ hasText: text });
  }

  columnHeader(name: string): Locator {
    return this.table.getByRole("columnheader", { name, exact: true });
  }

  sortButton(name: string): Locator {
    return this.columnHeader(name).getByRole("button", { name, exact: true });
  }

  async titles(): Promise<string[]> {
    if (await this.showsCards()) {
      return this.cards.getByTestId("data-table-card-title").allInnerTexts();
    }
    return this.tableRows.getByRole("rowheader").allInnerTexts();
  }

  async sortBy(header: string, choice: string): Promise<void> {
    if (await this.showsCards()) {
      await this.sortSelect.click();
      await this.page.getByRole("option", { name: choice, exact: true }).click();
      await expect(this.sortSelect).toHaveText(choice);
    } else {
      await this.sortButton(header).click();
    }
  }

  async choosePageSize(size: number): Promise<void> {
    await this.pageSize.click();
    await this.page.getByRole("option", { name: String(size), exact: true }).click();
  }

  async setColumnVisible(header: string, visible: boolean): Promise<void> {
    await this.columnsButton.click();
    const item = this.page.getByRole("menuitemcheckbox", { name: header, exact: true });
    await expect(item).toBeVisible();
    if ((await item.getAttribute("aria-checked")) !== String(visible)) await item.click();
    await expect(item).toHaveAttribute("aria-checked", String(visible));
    await this.page.keyboard.press("Escape");
    await expect(item).toBeHidden();
  }

  textFilter(label: string): Locator {
    return this.filters.getByRole("searchbox", { name: label, exact: true });
  }

  optionsFilter(label: string): Locator {
    return this.filters.getByRole("button", { name: new RegExp(`^${label} `) });
  }

  async chooseOptions(label: string, options: readonly string[]): Promise<void> {
    await this.optionsFilter(label).click();
    for (const option of options) {
      const item = this.page.getByRole("menuitemcheckbox", { name: option, exact: true });
      await item.click();
      await expect(item).toHaveAttribute("aria-checked", "true");
    }
    await this.page.keyboard.press("Escape");
    await expect(this.page.getByRole("menu")).toHaveCount(0);
  }

  rangeStart(label: string): Locator {
    return this.filters.getByRole("textbox", { name: `${label} From`, exact: true });
  }

  rangeEnd(label: string): Locator {
    return this.filters.getByRole("textbox", { name: `${label} To`, exact: true });
  }
}
