import { expect, type Locator, type Page, type Request } from "@playwright/test";

import type { ApprovalKindAnswer } from "../../../fixtures/records/approval-records";

import { approvalPagePaths, roleGroupText } from "./approvals.page";

export const kindsPath = approvalPagePaths.kinds;

export const managerStepRule =
  "Decided by the manager of the person who asks, as on the day they ask. A manager who has left, who was added by work email and has not signed in yet, or who submitted the request is passed over for the one above, and the administrators decide when nobody is left.";

export const workingDaysProblem = "Give each step 1 to 30 working days.";

// A Server Function call is a POST to the kinds' own page carrying the next-action header.
export function isKindSave(request: Request): boolean {
  return (
    request.method() === "POST" &&
    new URL(request.url()).pathname === kindsPath &&
    request.headers()["next-action"] !== undefined
  );
}

function workingDaysText(days: number): string {
  return days === 1 ? "1 working day" : `${days} working days`;
}

export function savedText(kind: ApprovalKindAnswer, required: boolean, days: number): string {
  return required
    ? `Saved ${kind.title}: each step of a new request has ${workingDaysText(days)}.`
    : `Saved ${kind.title}: its records now go through without a request.`;
}

// A kind's card, as the API's answer describes it: whether it needs approval, the working days in force beside the kind's own,
// and who decides each step.
export function kindSnapshot(kind: ApprovalKindAnswer, canChange: boolean): string {
  const required = kind.setting?.required ?? true;
  const days = kind.setting?.workingDaysToDecide ?? kind.declaredWorkingDays;
  const forEachStep = `${workingDaysText(days)} for each step`;
  const daysText =
    days === kind.declaredWorkingDays
      ? `${forEachStep}.`
      : `${forEachStep}, as an administrator set it; the ERP's own number is ${kind.declaredWorkingDays}.`;
  const approvalText = required
    ? "Each request goes through these steps, in this order."
    : "Records of this kind go through at once, without a request, so nobody but the person who made one looks at it. Once approval is switched on again, each request goes through these steps.";
  return [
    "- listitem:",
    `  - heading ${JSON.stringify(kind.title)} [level=3]`,
    `  - paragraph: ${JSON.stringify(kind.description)}`,
    ...(canChange ? [`  - button ${JSON.stringify(`Change ${kind.title}`)}: Change`] : []),
    `  - paragraph: ${JSON.stringify(`${required ? "Needs approval" : "Approval switched off"} ${daysText}`)}`,
    `  - paragraph: ${JSON.stringify(approvalText)}`,
    `  - list ${JSON.stringify(`Steps of ${kind.title}`)}:`,
    ...kind.steps.flatMap((step) => [
      "    - listitem:",
      `      - paragraph: ${JSON.stringify(`${step.order}. ${step.title}`)}`,
      `      - paragraph: ${JSON.stringify(
        step.rule === "role" && step.role !== null
          ? `Decided by ${roleGroupText(step.role)}.`
          : managerStepRule,
      )}`,
    ]),
  ].join("\n");
}

export function settingDialogSnapshot(kind: ApprovalKindAnswer, required: boolean, days: string): string {
  const title = `Change ${kind.title}`;
  return [
    `- dialog ${JSON.stringify(title)}:`,
    `  - heading ${JSON.stringify(title)} [level=2]`,
    "  - paragraph: The change applies to the requests asked for after it. A request already waiting keeps its steps and its due times.",
    "  - group:",
    "    - text: Needs approval",
    "    - paragraph: While it is off, each record of this kind goes through at once, without a request, so nobody but the person who made it looks at it.",
    `    - switch "Needs approval"${required ? " [checked]" : ""}`,
    "  - group:",
    "    - text: Working days for each step",
    `    - textbox "Working days for each step": ${JSON.stringify(days)}`,
    "    - paragraph: From 1 to 30, Monday to Friday, each ending at midnight India time.",
    '  - button "Cancel"',
    '  - button "Save the setting"',
    '  - button "Close"',
  ].join("\n");
}

export class ApprovalKindsPage {
  readonly main: Locator;
  readonly heading: Locator;
  readonly card: Locator;
  readonly list: Locator;
  readonly kinds: Locator;
  readonly empty: Locator;
  readonly unavailable: Locator;
  readonly dialog: Locator;
  readonly required: Locator;
  readonly days: Locator;
  readonly save: Locator;
  readonly cancel: Locator;
  readonly close: Locator;
  readonly summary: Locator;
  readonly reload: Locator;

  constructor(private readonly page: Page) {
    this.main = page.getByRole("main");
    this.heading = this.main.getByRole("heading", { level: 1 });
    this.card = page.getByTestId("approval-kinds-card");
    this.list = this.card.getByRole("list", { name: "Kinds the ERP asks approval for", exact: true });
    this.kinds = page.getByTestId("approval-kind");
    this.empty = page.getByTestId("approval-kinds-empty");
    this.unavailable = page.getByTestId("approval-kinds-unavailable");
    this.dialog = page.getByTestId("approval-kind-dialog");
    this.required = this.dialog.getByRole("switch", { name: "Needs approval", exact: true });
    this.days = this.dialog.getByRole("textbox", { name: "Working days for each step", exact: true });
    this.save = page.getByTestId("approval-kind-save");
    this.cancel = this.dialog.getByRole("button", { name: "Cancel", exact: true });
    this.close = this.dialog.getByRole("button", { name: "Close", exact: true });
    this.summary = this.dialog.locator("[data-slot='alert']");
    this.reload = page.getByTestId("approval-kind-reload");
  }

  async goto(): Promise<void> {
    await this.page.goto(kindsPath);
    await expect(this.page.getByRole("status", { name: /^Loading/ })).toHaveCount(0);
    await expect(this.heading).toBeVisible();
  }

  // Each kind's Change button opens a dialog, so it is disabled in the server's HTML and marks itself as rendering that state
  // until React hydrates it.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.main.locator("[data-hydrating]")).toHaveCount(0);
  }

  kind(title: string): Locator {
    return this.kinds.filter({ has: this.page.getByRole("heading", { name: title, exact: true, level: 3 }) });
  }

  change(title: string): Locator {
    return this.kind(title).getByTestId("approval-kind-change");
  }

  standing(title: string): Locator {
    return this.kind(title).getByTestId("approval-kind-standing");
  }

  workingDays(title: string): Locator {
    return this.kind(title).getByTestId("approval-kind-working-days");
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }

  async expectDialogClosed(): Promise<void> {
    await expect(this.dialog).toHaveCount(0);
  }
}
