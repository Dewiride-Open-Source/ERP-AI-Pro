import { expect, type Locator, type Page } from "@playwright/test";

import { DataTableRegion } from "../../shared/lists/data-table.page";

export const exchangeRatesPath = "/platform/reference-data/exchange-rates";

export const newExchangeRatePath = `${exchangeRatesPath}/new`;

export const importExchangeRatesPath = `${exchangeRatesPath}/import`;

export function exchangeRatePath(id: string): string {
  return `${exchangeRatesPath}/${id}`;
}

// The address the list's filter form writes for currencies and an inclusive run of days, as yyyy-MM-dd.
export function ratesOf(currencies: readonly string[], from: string, to: string): string {
  const search = new URLSearchParams();
  for (const currency of [...currencies].sort()) search.append("currency", currency);
  search.append("dayFrom", from);
  search.append("dayTo", to);
  return `?${search.toString()}`;
}

export class ExchangeRatesPage {
  readonly heading: Locator;
  readonly listCard: Locator;
  readonly listDescription: Locator;
  readonly list: DataTableRegion;
  readonly recordLink: Locator;
  readonly importLink: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    const main = page.getByRole("main");
    this.heading = main.getByRole("heading", { name: "Exchange rates", level: 1 });
    this.listCard = page.getByTestId("exchange-rates-list-card");
    this.listDescription = this.listCard.locator("[data-slot='card-description']");
    this.list = new DataTableRegion(page, this.listCard);
    this.recordLink = main.getByRole("link", { name: "Record a rate", exact: true });
    this.importLink = main.getByRole("link", { name: "Import rates", exact: true });
    this.unavailable = page.getByTestId("exchange-rates-unavailable");
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`${exchangeRatesPath}${query}`);
    await expect(this.heading).toBeVisible();
  }

  // The list's controls work only through React, so they are disabled in the server's HTML and mark themselves as rendering
  // that state until the page hydrates.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  // The list renders its rates twice, as a table and as cards, and shows one of them by the width of its card; a rate's
  // link is named after its currency and day, as "USD on 07-11-2026".
  row(name: string): Locator {
    return this.list.tableRows
      .or(this.list.cards)
      .filter({ visible: true })
      .filter({ has: this.page.getByRole("link", { name, exact: true }) });
  }

  rateLink(name: string): Locator {
    return this.row(name).getByRole("link", { name, exact: true });
  }
}
