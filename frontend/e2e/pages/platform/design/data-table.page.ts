import { expect, type Locator, type Page } from "@playwright/test";

import { DataTableRegion } from "../../shared/lists/data-table.page";

export const dataTablePath = "/design/data-table";

export class DataTableDemoPage {
  readonly heading: Locator;
  readonly listHeading: Locator;
  readonly bills: DataTableRegion;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Data table", level: 1 });
    this.listHeading = page.getByRole("heading", { name: "Purchase bills (example)", level: 2 });
    this.bills = new DataTableRegion(page, page.getByTestId("purchase-bills-card"));
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`${dataTablePath}${query}`);
    await expect(this.heading).toBeVisible();
    await this.bills.waitUntilInteractive();
  }
}
