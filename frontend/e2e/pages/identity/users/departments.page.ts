import { expect, type Locator, type Page } from "@playwright/test";

export const departmentsPath = "/identity/users/departments";

export class DepartmentsPage {
  readonly heading: Locator;
  readonly card: Locator;
  readonly listHeading: Locator;
  readonly addButton: Locator;
  readonly expandAll: Locator;
  readonly collapseAll: Locator;
  readonly unavailable: Locator;
  readonly dialog: Locator;
  readonly form: Locator;
  readonly summary: Locator;
  readonly name: Locator;
  readonly parent: Locator;
  readonly save: Locator;
  readonly cancel: Locator;
  readonly close: Locator;

  constructor(private readonly page: Page) {
    const main = page.getByRole("main");
    this.heading = main.getByRole("heading", { name: "Departments", level: 1 });
    this.card = page.getByTestId("departments-card");
    this.listHeading = this.card.getByRole("heading", { name: "All departments", level: 2 });
    this.addButton = page.getByTestId("departments-add");
    this.expandAll = page.getByTestId("departments-expand-all");
    this.collapseAll = page.getByTestId("departments-collapse-all");
    this.unavailable = page.getByTestId("departments-unavailable");
    this.dialog = page.getByRole("dialog").filter({ has: page.getByTestId("department-form") });
    this.form = page.getByTestId("department-form");
    this.summary = this.dialog.locator("[data-slot='alert']");
    this.name = this.dialog.getByRole("textbox", { name: "Name" });
    this.parent = this.dialog.getByRole("combobox", { name: "Sits under" });
    this.save = this.dialog.getByTestId("department-save");
    this.cancel = this.dialog.getByRole("button", { name: "Cancel" });
    this.close = this.dialog.getByRole("button", { name: "Close" });
  }

  async goto(): Promise<void> {
    await this.page.goto(departmentsPath);
    await expect(this.heading).toBeVisible();
  }

  // Every button of the tree is disabled in the server's HTML and marks itself as rendering that state until React
  // hydrates it.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  // A department's row holds its own name and controls; the departments under it follow in a list beside the row.
  row(name: string): Locator {
    return this.page
      .getByTestId("department-row")
      .filter({ has: this.page.getByTestId("department-name").getByText(name, { exact: true }) });
  }

  item(name: string): Locator {
    return this.row(name).locator("xpath=..");
  }

  childList(name: string): Locator {
    return this.item(name).locator(":scope > ul");
  }

  toggle(name: string): Locator {
    return this.row(name).getByRole("button", { name: `Show the departments under ${name}`, exact: true });
  }

  addUnder(name: string): Locator {
    return this.row(name).getByRole("button", { name: `Add a department under ${name}`, exact: true });
  }

  edit(name: string): Locator {
    return this.row(name).getByRole("button", { name: `Rename or move ${name}`, exact: true });
  }

  delete(name: string): Locator {
    return this.row(name).getByRole("button", { name: `Delete ${name}`, exact: true });
  }

  history(name: string): Locator {
    return this.row(name).getByRole("link", { name: `Change history of ${name}`, exact: true });
  }

  refusal(name: string): Locator {
    return this.row(name).getByTestId("department-delete-error");
  }

  confirmation(title: string): Locator {
    return this.page.getByRole("alertdialog", { name: title });
  }
}
