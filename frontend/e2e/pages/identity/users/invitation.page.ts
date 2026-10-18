import { expect, type Locator, type Page, type Request } from "@playwright/test";

import { PeoplePage, peoplePath } from "./people.page";

// The dialog's Server Function calls, the searches and the invitation, are POSTs to the People page that renders it.
export function isPeopleServerFunctionCall(request: Request): boolean {
  return (
    request.method() === "POST" &&
    new URL(request.url()).pathname === peoplePath &&
    request.headers()["next-action"] !== undefined
  );
}

export class InvitationDialog {
  readonly trigger: Locator;
  readonly dialog: Locator;
  readonly search: Locator;
  readonly status: Locator;
  readonly results: Locator;
  readonly failure: Locator;
  readonly cancel: Locator;
  readonly close: Locator;
  readonly chosen: Locator;
  readonly chosenName: Locator;
  readonly form: Locator;
  readonly summary: Locator;
  readonly employeeCode: Locator;
  readonly phoneNumber: Locator;
  readonly dateOfJoining: Locator;
  readonly roles: Locator;
  readonly back: Locator;
  readonly invite: Locator;

  constructor(page: Page) {
    this.trigger = new PeoplePage(page).inviteButton;
    this.dialog = page.getByRole("dialog", { name: "Invite from the directory" });
    this.search = this.dialog.getByRole("searchbox", { name: "Search the company directory" });
    this.status = this.dialog.getByTestId("directory-search-status");
    this.results = this.dialog.getByRole("list", { name: "People in the directory" });
    this.failure = this.dialog.getByTestId("directory-search").locator("[data-slot='alert']");
    this.cancel = this.dialog.getByRole("button", { name: "Cancel", exact: true });
    this.close = this.dialog.getByRole("button", { name: "Close", exact: true });
    this.chosen = this.dialog.getByTestId("invite-person-chosen");
    this.chosenName = this.chosen.getByRole("heading", { level: 3 });
    this.form = this.dialog.getByTestId("invite-person-form");
    this.summary = this.form.locator("[data-slot='alert']");
    this.employeeCode = this.form.getByRole("textbox", { name: "Employee code" });
    this.phoneNumber = this.form.getByRole("textbox", { name: "Phone number" });
    this.dateOfJoining = this.form.locator("input[id$='-dateOfJoining']");
    this.roles = this.form.getByRole("group", { name: "Roles in the ERP" });
    this.back = this.dialog.getByRole("button", { name: "Back to the search", exact: true });
    this.invite = this.dialog.getByTestId("invite-person-save");
  }

  // The trigger is disabled in the server-rendered page and enabled once React has hydrated it, when the dialog can open,
  // and the open dialog hands focus to its search field.
  async open(): Promise<void> {
    await expect(this.trigger).toBeEnabled();
    await this.trigger.click();
    await expect(this.dialog).toBeVisible();
    await expect(this.search).toBeFocused();
  }

  async find(text: string, found: string): Promise<void> {
    await this.search.fill(text);
    await expect(this.status).toHaveText(found);
  }

  // A person to choose is a button named by their display name and sign-in name; one the ERP already holds is a link to
  // their page named by the display name alone.
  choice(displayName: string): Locator {
    return this.results.getByRole("button", { name: displayName });
  }

  registeredLink(displayName: string): Locator {
    return this.results.getByRole("link", { name: displayName, exact: true });
  }

  role(name: string): Locator {
    return this.roles.getByRole("checkbox", { name, exact: true });
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }
}
