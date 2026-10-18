import { expect, type Locator, type Page } from "@playwright/test";

export const designationsPath = "/identity/users/designations";

export class DesignationsPage {
  readonly heading: Locator;
  readonly card: Locator;
  readonly listHeading: Locator;
  readonly addButton: Locator;
  readonly unavailable: Locator;
  readonly dialog: Locator;
  readonly summary: Locator;
  readonly name: Locator;
  readonly save: Locator;
  readonly cancel: Locator;
  readonly close: Locator;

  constructor(private readonly page: Page) {
    const main = page.getByRole("main");
    this.heading = main.getByRole("heading", { name: "Designations", level: 1 });
    this.card = page.getByTestId("designations-card");
    this.listHeading = this.card.getByRole("heading", { name: "All designations", level: 2 });
    this.addButton = page.getByTestId("designations-add");
    this.unavailable = page.getByTestId("designations-unavailable");
    this.dialog = page.getByRole("dialog").filter({ has: page.getByTestId("designation-form") });
    this.summary = this.dialog.locator("[data-slot='alert']");
    this.name = this.dialog.getByRole("textbox", { name: "Name" });
    this.save = this.dialog.getByTestId("designation-save");
    this.cancel = this.dialog.getByRole("button", { name: "Cancel" });
    this.close = this.dialog.getByRole("button", { name: "Close" });
  }

  async goto(): Promise<void> {
    await this.page.goto(designationsPath);
    await expect(this.heading).toBeVisible();
  }

  // Every button that opens a dialog is disabled in the server's HTML and marks itself as rendering that state until React
  // hydrates it.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  row(name: string): Locator {
    return this.page
      .getByTestId("designation")
      .filter({ has: this.page.getByTestId("designation-name").getByText(name, { exact: true }) });
  }

  rename(name: string): Locator {
    return this.row(name).getByRole("button", { name: `Rename ${name}`, exact: true });
  }

  delete(name: string): Locator {
    return this.row(name).getByRole("button", { name: `Delete ${name}`, exact: true });
  }

  history(name: string): Locator {
    return this.row(name).getByRole("link", { name: `Change history of ${name}`, exact: true });
  }

  refusal(name: string): Locator {
    return this.row(name).getByTestId("designation-delete-error");
  }

  confirmation(title: string): Locator {
    return this.page.getByRole("alertdialog", { name: title });
  }

  // Other workers add designations at the same time, so a test reads the order of its own designations only.
  async namesStartingWith(prefix: string): Promise<string[]> {
    const names = await this.card.getByTestId("designation-name").allInnerTexts();
    return names.filter((name) => name.startsWith(prefix));
  }
}
