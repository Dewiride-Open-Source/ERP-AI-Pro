import { expect, type Locator, type Page } from "@playwright/test";

import { DataTableRegion } from "../../shared/lists/data-table.page";

export const peoplePath = "/identity/users";

export const peopleApi = "/api/identity/users";

export class PeoplePage {
  readonly heading: Locator;
  readonly listHeading: Locator;
  readonly listCard: Locator;
  readonly list: DataTableRegion;
  readonly registerLink: Locator;
  readonly permissionsLink: Locator;
  readonly empty: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "People", level: 1 });
    this.listHeading = page.getByRole("heading", { name: "All people", level: 2 });
    this.listCard = page.getByTestId("people-list-card");
    this.list = new DataTableRegion(page, this.listCard);
    this.registerLink = page.getByRole("main").getByRole("link", { name: "Register a person", exact: true });
    this.permissionsLink = page
      .getByRole("main")
      .getByRole("link", { name: "Roles and permissions", exact: true });
    this.empty = page.getByTestId("people-empty");
    this.unavailable = page.getByTestId("people-unavailable");
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`${peoplePath}${query}`);
    await expect(this.heading).toBeVisible();
  }

  async waitUntilInteractive(): Promise<void> {
    await this.list.waitUntilInteractive();
  }

  // The list renders its people twice, as a table and as cards, and shows one of them by the width of its card.
  row(displayName: string): Locator {
    return this.list.tableRows
      .or(this.list.cards)
      .filter({ visible: true })
      .filter({ has: this.page.getByTestId("people-name").getByText(displayName, { exact: true }) });
  }

  nameLink(displayName: string): Locator {
    return this.row(displayName).getByRole("link", { name: displayName, exact: true });
  }
}
