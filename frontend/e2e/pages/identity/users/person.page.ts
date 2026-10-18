import { expect, type Locator, type Page } from "@playwright/test";

import { peoplePath } from "./people.page";

export function personPath(id: string): string {
  return `${peoplePath}/${id}`;
}

export function typedDate(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  return `${day}-${month}-${year}`;
}

const calendarDayName = new Intl.DateTimeFormat("en-GB", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: "UTC",
});

export class PersonPage {
  readonly heading: Locator;
  readonly header: Locator;
  readonly status: Locator;
  readonly actions: Locator;
  readonly editLink: Locator;
  readonly statusAction: Locator;
  readonly statusError: Locator;
  readonly deleteAction: Locator;
  readonly deleteError: Locator;
  readonly details: Locator;
  readonly roles: Locator;
  readonly roleList: Locator;
  readonly assignRoles: Locator;
  readonly rolesDialog: Locator;
  readonly rolesSummary: Locator;
  readonly rolesCancel: Locator;
  readonly rolesClose: Locator;
  readonly saveRoles: Locator;
  readonly moves: Locator;
  readonly movesHeading: Locator;
  readonly moveItems: Locator;
  readonly movesNone: Locator;
  readonly recordMove: Locator;
  readonly moveDialog: Locator;
  readonly moveSummary: Locator;
  readonly moveDate: Locator;
  readonly moveDateTrigger: Locator;
  readonly moveCalendar: Locator;
  readonly moveDepartment: Locator;
  readonly moveDesignation: Locator;
  readonly moveManager: Locator;
  readonly moveCancel: Locator;
  readonly moveClose: Locator;
  readonly saveMove: Locator;
  readonly history: Locator;
  readonly historyLink: Locator;
  readonly unavailable: Locator;

  constructor(private readonly page: Page) {
    const main = page.getByRole("main");
    this.heading = main.getByRole("heading", { level: 1 });
    this.header = main.locator("header").first();
    this.status = page.getByTestId("person-status");
    this.actions = page.getByTestId("person-actions");
    this.editLink = this.actions.getByRole("link", { name: "Edit details" });
    this.statusAction = page.getByTestId("person-status-action");
    this.statusError = page.getByTestId("person-status-error");
    this.deleteAction = page.getByTestId("person-delete-action");
    this.deleteError = page.getByTestId("person-delete-error");
    this.details = page.getByTestId("person-details");
    this.roles = page.getByTestId("person-roles");
    this.roleList = this.roles.getByRole("list", { name: "Roles" });
    this.assignRoles = page.getByTestId("assign-roles");
    this.rolesDialog = page.getByRole("dialog", { name: "Assign roles" });
    this.rolesSummary = this.rolesDialog.locator("[data-slot='alert']");
    this.rolesCancel = this.rolesDialog.getByRole("button", { name: "Cancel" });
    this.rolesClose = this.rolesDialog.getByRole("button", { name: "Close" });
    this.saveRoles = this.rolesDialog.getByTestId("assign-roles-save");
    this.moves = page.getByTestId("person-moves");
    this.movesHeading = this.moves.getByRole("heading", { name: "Moves", level: 2 });
    this.moveItems = this.moves.getByTestId("person-move");
    this.movesNone = this.moves.getByTestId("person-moves-none");
    this.recordMove = page.getByTestId("record-move");
    this.moveDialog = page.getByRole("dialog", { name: "Record a move" });
    this.moveSummary = this.moveDialog.locator("[data-slot='alert']");
    // While the calendar is open the rest of the page is hidden from assistive technology, so the date box and its trigger
    // are found by the end of the field id and by label attribute rather than by role.
    const moveForm = page.getByTestId("record-move-form");
    this.moveDate = moveForm.locator("input[id$='-effectiveFrom']");
    this.moveDateTrigger = moveForm.locator("button[aria-label='Choose date']");
    this.moveCalendar = page.getByRole("dialog", { name: "Choose date", exact: true });
    this.moveDepartment = this.moveDialog.getByRole("combobox", { name: "Department" });
    this.moveDesignation = this.moveDialog.getByRole("combobox", { name: "Designation" });
    this.moveManager = this.moveDialog.getByRole("combobox", { name: "Manager" });
    this.moveCancel = this.moveDialog.getByRole("button", { name: "Cancel" });
    this.moveClose = this.moveDialog.getByRole("button", { name: "Close" });
    this.saveMove = this.moveDialog.getByTestId("record-move-save");
    this.history = page.getByTestId("record-history");
    this.historyLink = page.getByTestId("record-history-link");
    this.unavailable = page.getByTestId("person-unavailable");
  }

  async goto(id: string): Promise<void> {
    await this.page.goto(personPath(id));
    await expect(this.heading).toBeVisible();
  }

  // Every button of the page that opens a dialog is disabled in the server's HTML and marks itself as rendering that state
  // until React hydrates it, when its dialog can open.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.page.getByRole("main").locator("[data-hydrating]")).toHaveCount(0);
  }

  roleBox(name: string): Locator {
    return this.rolesDialog.getByRole("checkbox", { name, exact: true });
  }

  move(title: string): Locator {
    return this.moveItems.filter({ has: this.page.getByRole("heading", { name: title, exact: true }) });
  }

  withdrawButton(isoDate: string): Locator {
    return this.moves.getByRole("button", {
      name: `Withdraw the move from ${typedDate(isoDate)}`,
      exact: true,
    });
  }

  // A day's name in the calendar starts with its weekday and may end with its state, such as ", selected".
  calendarDay(isoDate: string): Locator {
    const day = calendarDayName.format(new Date(`${isoDate}T00:00:00Z`));
    return this.moveCalendar.getByRole("button", { name: new RegExp(`\\b${day}\\b`) });
  }

  confirmation(title: string): Locator {
    return this.page.getByRole("alertdialog", { name: title });
  }

  // The search text is unique to the test, so the list it leaves holds the one option to choose, which the keyboard then
  // makes active and chooses; an option's name also holds its description, such as a manager's work email.
  async choose(combobox: Locator, search: string, option: string): Promise<void> {
    await combobox.fill(search);
    const list = this.page.getByRole("listbox");
    await expect(list.getByRole("option")).toHaveCount(1);
    await expect(list.getByRole("option", { name: option })).toBeVisible();
    await combobox.press("ArrowDown");
    await expect(list.getByRole("option", { name: option })).toHaveAttribute("aria-selected", "true");
    await combobox.press("Enter");
    await expect(list).toBeHidden();
    await expect(combobox).toHaveValue(option);
  }
}
