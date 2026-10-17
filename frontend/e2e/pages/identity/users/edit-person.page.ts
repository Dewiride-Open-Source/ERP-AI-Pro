import { expect, type Locator, type Page } from "@playwright/test";

import { personPath } from "./person.page";

export function editPersonPath(id: string): string {
  return `${personPath(id)}/edit`;
}

export class EditPersonPage {
  readonly heading: Locator;
  readonly form: Locator;
  readonly summary: Locator;
  readonly identity: Locator;
  readonly employeeCode: Locator;
  readonly phoneNumber: Locator;
  readonly dateOfJoining: Locator;
  readonly save: Locator;
  readonly cancel: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("main").getByRole("heading", { level: 1 });
    this.form = page.getByRole("form", { name: "Details kept in the ERP" });
    this.summary = this.form.locator("[data-slot='alert']");
    this.identity = page.getByTestId("edit-person-identity");
    this.employeeCode = this.form.getByRole("textbox", { name: "Employee code" });
    this.phoneNumber = this.form.getByRole("textbox", { name: "Phone number" });
    this.dateOfJoining = page.getByTestId("edit-person-form").locator("input[id$='-dateOfJoining']");
    this.save = this.form.getByTestId("edit-person-save");
    this.cancel = this.form.getByRole("link", { name: "Cancel" });
    this.unavailable = page.getByTestId("edit-person-unavailable");
  }

  // Save is disabled in the server-rendered page and enabled once React has hydrated the form, so it marks the form as live.
  async goto(id: string): Promise<void> {
    await this.page.goto(editPersonPath(id));
    await expect(this.heading).toBeVisible();
    await expect(this.save).toBeEnabled();
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }
}
