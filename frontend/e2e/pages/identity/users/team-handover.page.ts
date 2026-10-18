import type { Locator, Page } from "@playwright/test";

// Deactivating a manager and handing over a deactivated manager's team open the same step, under the title and with the
// cancel button of each.
export class TeamHandoverDialog {
  readonly dialog: Locator;
  readonly members: Locator;
  readonly choice: Locator;
  readonly anotherPerson: Locator;
  readonly noManager: Locator;
  readonly manager: Locator;
  readonly summary: Locator;
  readonly cancel: Locator;
  readonly close: Locator;
  readonly save: Locator;

  constructor(page: Page, { title, cancel }: { readonly title: string; readonly cancel: string }) {
    this.dialog = page.getByRole("dialog", { name: title });
    this.members = this.dialog.getByTestId("team-handover-members");
    this.choice = this.dialog.getByRole("radiogroup", { name: "Who they report to from today" });
    this.anotherPerson = this.choice.getByRole("radio", { name: "Another person", exact: true });
    this.noManager = this.choice.getByRole("radio", { name: "No manager", exact: true });
    this.manager = this.dialog.getByRole("combobox", { name: "New manager" });
    this.summary = this.dialog.locator("[data-slot='alert']");
    this.cancel = this.dialog.getByRole("button", { name: cancel, exact: true });
    this.close = this.dialog.getByRole("button", { name: "Close", exact: true });
    this.save = this.dialog.getByTestId("team-handover-save");
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field' or @data-slot='field-set'][1]");
  }
}
