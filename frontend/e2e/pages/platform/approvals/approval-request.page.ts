import { expect, type Locator, type Page, type Request } from "@playwright/test";

import type {
  ApprovalRequest,
  ApprovalStep,
  ApprovalStepState,
} from "../../../fixtures/records/approval-records";

import {
  approvalRequestPath,
  approvalStateLabels,
  dateTimeText,
  dueDayText,
  lateLabel,
  personText,
  roleGroupText,
  waitingText,
} from "./approvals.page";

export type RequestAction = "approve" | "return" | "reject" | "withdraw";

export const actionButtons: Readonly<Record<RequestAction, string>> = {
  approve: "Approve",
  return: "Send back",
  reject: "Reject",
  withdraw: "Withdraw",
};

export const actionTitles: Readonly<Record<RequestAction, string>> = {
  approve: "Approve this request",
  return: "Send back this request",
  reject: "Reject this request",
  withdraw: "Withdraw this request",
};

const stepStateLabels: Readonly<Record<ApprovalStepState, string>> = {
  pending: "Not reached yet",
  waiting: "Waiting",
  approved: "Approved",
  returned: "Sent back",
  rejected: "Rejected",
  closed: "Not decided",
};

const commentLabels: Readonly<Record<ApprovalStepState, string>> = {
  pending: "Note",
  waiting: "Note",
  approved: "Note",
  returned: "What needs to change",
  rejected: "Why it is rejected",
  closed: "Why it was withdrawn",
};

const decisionVerbs: Readonly<Partial<Record<ApprovalStepState, string>>> = {
  approved: "Approved",
  returned: "Sent back",
  rejected: "Rejected",
};

export const optionalNoteHint =
  "Optional, up to 1,000 characters. Everyone who can see the request can read it.";

export const reasonHint = "Up to 1,000 characters. Everyone who can see the request can read it.";

// A Server Function call is a POST to the request's own page carrying the next-action header.
export function isRequestAction(request: Request, id: string): boolean {
  return (
    request.method() === "POST" &&
    new URL(request.url()).pathname === approvalRequestPath(id) &&
    request.headers()["next-action"] !== undefined
  );
}

export function inPlaceNotice(approver: string): string {
  return `You decide in place of ${approver}. The request's history will say so.`;
}

export function approverText(step: ApprovalStep, viewer: string): string {
  if (step.approverKind === "role" && step.approverRole !== null) return roleGroupText(step.approverRole);
  return personText(step.approverId ?? "", step.approverName, viewer);
}

// What the status line under the request's title says: its standing, then the step it waits at or when it closed.
export function statusLineText(request: ApprovalRequest, viewer: string): string {
  const standing = [approvalStateLabels[request.state], ...(request.late ? [lateLabel] : [])].join(" ");
  if (request.state === "submitted") return `${standing} ${waitingText(request, viewer)}`;
  return request.closedAt === null ? standing : `${standing} on ${dateTimeText(request.closedAt)}`;
}

export function stepSentence(step: ApprovalStep, request: ApprovalRequest, viewer: string): string {
  const approver = approverText(step, viewer);
  switch (step.state) {
    case "pending":
      return `Waits for ${approver} once the steps before it are approved.`;
    case "waiting":
      return step.startedAt === null
        ? `Waiting for ${approver}.`
        : `Waiting for ${approver} since ${dateTimeText(step.startedAt)}.`;
    case "closed":
      if (request.state !== "withdrawn") return "Not decided: an earlier step closed the request.";
      return step.startedAt === null
        ? "Not decided: the request was withdrawn before it reached this step."
        : `Not decided: the request was withdrawn while this step waited for ${approver}.`;
    default: {
      const decider =
        step.decidedById === null ? "" : ` by ${personText(step.decidedById, step.decidedByName, viewer)}`;
      const when = step.decidedAt === null ? "" : ` on ${dateTimeText(step.decidedAt)}`;
      const inPlace = step.inPlaceOfApprover ? `, in place of ${approver}` : "";
      return `${decisionVerbs[step.state] ?? stepStateLabels[step.state]}${decider}${when}${inPlace}.`;
    }
  }
}

function workingDaysText(days: number): string {
  return days === 1 ? "1 working day" : `${days} working days`;
}

// A step as the request page lists it: its title and standing, what became of it, its due time while it waits, the days it will
// have while it is not reached, and the note or reason it was decided with.
export function stepSnapshot(step: ApprovalStep, request: ApprovalRequest, viewer: string): string[] {
  const standing = [stepStateLabels[step.state], ...(step.late ? [lateLabel] : [])].join(" ");
  const due =
    step.state === "waiting" && step.dueAt !== null
      ? [`${step.late ? "Was due" : "Due"} by the end of ${dueDayText(step.dueAt)}.`]
      : [];
  const days =
    step.state === "pending"
      ? [`${workingDaysText(step.workingDays)} to decide, once it starts waiting.`]
      : [];
  const comment = step.comment === null ? [] : [commentLabels[step.state], step.comment];
  return [
    "- listitem:",
    `  - heading ${JSON.stringify(`${step.order}. ${step.title}`)} [level=3]`,
    `  - text: ${JSON.stringify(standing)}`,
    ...[stepSentence(step, request, viewer), ...due, ...days, ...comment].map(
      (text) => `  - paragraph: ${JSON.stringify(text)}`,
    ),
  ];
}

export function stepsSnapshot(request: ApprovalRequest, viewer: string): string {
  return [
    '- list "Steps":',
    ...request.steps.flatMap((step) => stepSnapshot(step, request, viewer).map((line) => `  ${line}`)),
  ].join("\n");
}

// The request's details: who asked, when, and its due time while it waits or the time it closed.
export function detailsSnapshot(request: ApprovalRequest, viewer: string): string {
  const askedBy = personText(request.requestedForId, request.requestedForName, viewer, true);
  const submittedBy =
    request.submittedById.toLowerCase() === request.requestedForId.toLowerCase()
      ? askedBy
      : `${askedBy}, submitted by ${personText(request.submittedById, request.submittedByName, viewer)}`;
  const due =
    request.state === "submitted" && request.dueAt !== null
      ? [
          "- term: Due",
          `- definition: ${JSON.stringify(`By the end of ${dueDayText(request.dueAt)}${request.late ? ` ${lateLabel}` : ""}`)}`,
        ]
      : [];
  const closed =
    request.closedAt === null
      ? []
      : ["- term: Closed", `- definition: ${JSON.stringify(dateTimeText(request.closedAt))}`];
  return [
    '- heading "Details" [level=2]',
    "- text: Who asked for this approval and when, in India Standard Time.",
    "- term: Asked by",
    `- definition: ${JSON.stringify(submittedBy)}`,
    "- term: Asked on",
    `- definition: ${JSON.stringify(dateTimeText(request.submittedAt))}`,
    ...due,
    ...closed,
  ].join("\n");
}

export function headerSnapshot(
  request: ApprovalRequest,
  viewer: string,
  buttons: readonly RequestAction[],
  inPlaceOf?: string,
): string {
  return [
    "- paragraph: Approvals",
    `- heading ${JSON.stringify(request.kindTitle)} [level=1]`,
    `- paragraph: ${JSON.stringify(statusLineText(request, viewer))}`,
    ...(inPlaceOf === undefined ? [] : [`- paragraph: ${JSON.stringify(inPlaceNotice(inPlaceOf))}`]),
    ...buttons.map((action) => `- button ${JSON.stringify(actionButtons[action])}`),
  ].join("\n");
}

export function dialogSnapshot(action: RequestAction, description: string, inPlaceOf?: string): string {
  const field =
    action === "return"
      ? { label: "What needs to change?", hint: reasonHint }
      : action === "reject"
        ? { label: "Why is it rejected?", hint: reasonHint }
        : { label: action === "withdraw" ? "Reason" : "Note", hint: optionalNoteHint };
  return [
    `- dialog ${JSON.stringify(actionTitles[action])}:`,
    `  - heading ${JSON.stringify(actionTitles[action])} [level=2]`,
    `  - paragraph: ${JSON.stringify(description)}`,
    ...(inPlaceOf === undefined ? [] : [`  - paragraph: ${JSON.stringify(inPlaceNotice(inPlaceOf))}`]),
    "  - group:",
    `    - text: ${JSON.stringify(field.label)}`,
    `    - textbox ${JSON.stringify(field.label)}`,
    `    - paragraph: ${JSON.stringify(field.hint)}`,
    '  - button "Cancel"',
    `  - button ${JSON.stringify(actionButtons[action])}`,
    '  - button "Close"',
  ].join("\n");
}

export class ApprovalRequestPage {
  readonly main: Locator;
  readonly root: Locator;
  readonly heading: Locator;
  readonly statusLine: Locator;
  readonly inPlace: Locator;
  readonly actions: Locator;
  readonly approve: Locator;
  readonly sendBack: Locator;
  readonly reject: Locator;
  readonly withdraw: Locator;
  readonly details: Locator;
  readonly steps: Locator;
  readonly stepList: Locator;
  readonly earlier: Locator;
  readonly earlierRequests: Locator;
  readonly unavailable: Locator;
  readonly dialog: Locator;
  readonly dialogInPlace: Locator;
  readonly comment: Locator;
  readonly submit: Locator;
  readonly cancel: Locator;
  readonly close: Locator;
  readonly summary: Locator;
  readonly reload: Locator;

  constructor(private readonly page: Page) {
    this.main = page.getByRole("main");
    this.root = page.getByTestId("approval-request");
    this.heading = this.main.getByRole("heading", { level: 1 });
    this.statusLine = page.getByTestId("approval-request-status-line");
    this.inPlace = page.getByTestId("approval-in-place-notice");
    this.actions = page.getByTestId("approval-request-actions");
    this.approve = page.getByTestId("approval-approve");
    this.sendBack = page.getByTestId("approval-return");
    this.reject = page.getByTestId("approval-reject");
    this.withdraw = page.getByTestId("approval-withdraw");
    this.details = page.getByTestId("approval-request-details");
    this.steps = page.getByTestId("approval-request-steps");
    this.stepList = this.steps.getByRole("list", { name: "Steps", exact: true });
    this.earlier = page.getByTestId("approval-earlier-requests");
    this.earlierRequests = this.earlier.getByTestId("approval-earlier-request");
    this.unavailable = page.getByTestId("approval-request-unavailable");
    this.dialog = page.getByTestId("approval-decision-dialog");
    this.dialogInPlace = this.dialog.getByTestId("approval-decision-in-place");
    this.comment = page.getByTestId("approval-decision-comment");
    this.submit = page.getByTestId("approval-decision-submit");
    this.cancel = this.dialog.getByRole("button", { name: "Cancel", exact: true });
    this.close = this.dialog.getByRole("button", { name: "Close", exact: true });
    this.summary = this.dialog.locator("[data-slot='alert']");
    this.reload = page.getByTestId("approval-decision-reload");
  }

  async goto(id: string): Promise<void> {
    await this.page.goto(approvalRequestPath(id));
    await expect(this.heading).toBeVisible();
  }

  // Every button opens a dialog, so each is disabled in the server's HTML and marks itself as rendering that state until React
  // hydrates it.
  async waitUntilInteractive(): Promise<void> {
    await expect(this.main.locator("[data-hydrating]")).toHaveCount(0);
  }

  button(action: RequestAction): Locator {
    return this.page.getByTestId(`approval-${action}`);
  }

  step(order: number): Locator {
    return this.steps.locator(`[data-testid='approval-step'][data-step='${order}']`);
  }

  fieldOf(control: Locator): Locator {
    return control.locator("xpath=ancestor::*[@data-slot='field'][1]");
  }

  // A dialog that starts to close turns its data-state to "closed" at once but stays on the page through its exit animation, so
  // whether it closed is judged by its count once it has gone.
  async expectDialogClosed(): Promise<void> {
    await expect(this.dialog).toHaveCount(0);
  }
}
