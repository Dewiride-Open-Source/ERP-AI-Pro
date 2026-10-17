import { expect, type Locator, type Page, type Request } from "@playwright/test";

export const registrationPath = "/identity/users/new";

// A Server Function call is a POST to the page that renders the form.
export function isRegistrationRequest(request: Request): boolean {
  return (
    request.method() === "POST" &&
    new URL(request.url()).pathname === registrationPath &&
    request.headers()["next-action"] !== undefined
  );
}

export class RegistrationPage {
  readonly heading: Locator;
  readonly form: Locator;
  readonly summary: Locator;
  readonly displayName: Locator;
  readonly workEmail: Locator;
  readonly employeeCode: Locator;
  readonly phoneNumber: Locator;
  readonly dateOfJoining: Locator;
  readonly roles: Locator;
  readonly save: Locator;
  readonly cancel: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Register a person", level: 1 });
    this.form = page.getByRole("form", { name: "Their details" });
    this.summary = this.form.locator("[data-slot='alert']");
    this.displayName = this.form.getByRole("textbox", { name: "Name", exact: true });
    this.workEmail = this.form.getByRole("textbox", { name: "Work email" });
    this.employeeCode = this.form.getByRole("textbox", { name: "Employee code" });
    this.phoneNumber = this.form.getByRole("textbox", { name: "Phone number" });
    this.dateOfJoining = page.getByTestId("register-person-form").locator("input[id$='-dateOfJoining']");
    this.roles = this.form.getByRole("group", { name: "Roles in the ERP" });
    this.save = this.form.getByTestId("register-person-save");
    this.cancel = this.form.getByRole("link", { name: "Cancel" });
  }

  // Register is disabled in the server-rendered page and enabled once React has hydrated the form, so it marks the form as
  // live.
  async goto(): Promise<void> {
    await this.page.goto(registrationPath);
    await expect(this.heading).toBeVisible();
    await expect(this.save).toBeEnabled();
  }

  role(name: string): Locator {
    return this.roles.getByRole("checkbox", { name, exact: true });
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }
}
