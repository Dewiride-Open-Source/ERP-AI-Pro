import { expect, type Locator, type Page } from "@playwright/test";

export class AccountCheckDialog {
  readonly trigger: Locator;
  readonly dialog: Locator;
  readonly status: Locator;
  readonly deactivated: Locator;
  readonly error: Locator;
  readonly cancel: Locator;
  readonly run: Locator;
  readonly done: Locator;
  readonly close: Locator;

  constructor(page: Page) {
    this.trigger = page
      .getByRole("main")
      .getByRole("button", { name: "Check Microsoft accounts", exact: true });
    this.dialog = page.getByRole("dialog", { name: "Check Microsoft accounts" });
    this.status = this.dialog.getByTestId("account-check-status");
    this.deactivated = this.dialog.getByRole("list", { name: "People the check deactivated" });
    this.error = this.dialog.getByTestId("account-check-error");
    this.cancel = this.dialog.getByRole("button", { name: "Cancel", exact: true });
    this.run = this.dialog.getByRole("button", { name: "Check", exact: true });
    this.done = this.dialog.getByRole("button", { name: "Done", exact: true });
    this.close = this.dialog.getByRole("button", { name: "Close", exact: true });
  }

  // The trigger is disabled in the server-rendered page and enabled once React has hydrated it, when the dialog can open.
  async open(): Promise<void> {
    await expect(this.trigger).toBeEnabled();
    await this.trigger.click();
    await expect(this.dialog).toBeVisible();
  }

  deactivatedPerson(displayName: string): Locator {
    return this.deactivated.getByRole("link", { name: displayName, exact: true });
  }
}
