import type {
  ApprovalState,
  ApprovalStepState,
  ApprovalSubmitterKind,
  ApproverKind,
  ErpRole,
} from "@dewiride/erp-api-client";

import { formatDateTimeIst, formatDayIst } from "@/shared/format/dates";

export interface PersonReference {
  readonly id: string | undefined;
  readonly name: string | undefined;
}

export interface Approver extends PersonReference {
  readonly kind: ApproverKind;
  readonly role: ErpRole | undefined;
}

export interface RequestCounts {
  readonly waitingForMe: number;
  readonly lateForMe: number;
  readonly mineWaiting: number;
}

export interface StepFacts {
  readonly state: ApprovalStepState;
  readonly approver: string;
  readonly decidedBy: string | undefined;
  readonly decidedAt: Date | undefined;
  readonly startedAt: Date | undefined;
  readonly inPlaceOfApprover: boolean;
}

export const lateLabel = "Late";

export const notRecorded = "—";

export const kindsUnavailableNote =
  "The kinds of request could not be read, so the list cannot be filtered by kind of request now.";

const stateLabels: Readonly<Record<ApprovalState, string>> = {
  submitted: "Waiting",
  approved: "Approved",
  returned: "Sent back",
  rejected: "Rejected",
  withdrawn: "Withdrawn",
};

const stepStateLabels: Readonly<Record<ApprovalStepState, string>> = {
  pending: "Not reached yet",
  waiting: "Waiting",
  approved: "Approved",
  returned: "Sent back",
  rejected: "Rejected",
  closed: "Not decided",
};

// A role step waits for everyone who holds the role, so it is named after the people who hold it.
const roleGroups: Readonly<Record<ErpRole, { readonly start: string; readonly within: string }>> = {
  employee: { start: "The employees", within: "the employees" },
  finance: { start: "Finance", within: "Finance" },
  hr: { start: "HR", within: "HR" },
  manager: { start: "The managers", within: "the managers" },
  administrator: { start: "The administrators", within: "the administrators" },
};

const decisionVerbs: Readonly<Partial<Record<ApprovalStepState, string>>> = {
  approved: "Approved",
  returned: "Sent back",
  rejected: "Rejected",
};

const commentLabels: Readonly<Record<ApprovalStepState, string>> = {
  pending: "Note",
  waiting: "Note",
  approved: "Note",
  returned: "What needs to change",
  rejected: "Why it is rejected",
  closed: "Why it was withdrawn",
};

const indianCount = new Intl.NumberFormat("en-IN");

const indiaDate = new Intl.DateTimeFormat("en-IN", { timeZone: "Asia/Kolkata", dateStyle: "medium" });

export function approvalStateLabel(state: ApprovalState): string {
  return stateLabels[state];
}

export function stepStateLabel(state: ApprovalStepState): string {
  return stepStateLabels[state];
}

export function roleGroupText(role: ErpRole, sentenceStart = false): string {
  const group = roleGroups[role];
  return sentenceStart ? group.start : group.within;
}

export function sameId(left: string, right: string): boolean {
  return left.toLowerCase() === right.toLowerCase();
}

// The person reading the page is "you", whatever name the ERP knows for them; a name is never re-cased, so a sentence that
// starts with one keeps it as the person's record spells it.
export function personText(
  person: PersonReference,
  viewer: string | undefined,
  sentenceStart = false,
): string {
  const { id, name } = person;
  if (id !== undefined && viewer !== undefined && sameId(id, viewer)) return sentenceStart ? "You" : "you";
  return name ?? (sentenceStart ? "An unknown person" : "an unknown person");
}

// The ERP itself submits a request when one of its jobs asks for approval, and the API says so with the submitter's kind.
export function submitterText(
  kind: ApprovalSubmitterKind | null | undefined,
  person: PersonReference,
  viewer: string | undefined,
): string {
  return kind === "system" ? "the ERP" : personText(person, viewer);
}

export function approverText(approver: Approver, viewer: string | undefined, sentenceStart = false): string {
  return approver.kind === "role" && approver.role !== undefined
    ? roleGroupText(approver.role, sentenceStart)
    : personText(approver, viewer, sentenceStart);
}

// A step is due by the end of a working day in India and late from the midnight that ends it, which is the due time the API
// sends, so the day a person reads is the one that midnight closes.
export function lastDayToDecide(dueAt: Date): Date {
  return new Date(dueAt.getTime() - 1);
}

export function dueDateText(dueAt: Date): string {
  return indiaDate.format(lastDayToDecide(dueAt));
}

export function dueSentence(dueAt: Date, late: boolean): string {
  const day = formatDayIst(lastDayToDecide(dueAt));
  return late ? `Was due by the end of ${day}.` : `Due by the end of ${day}.`;
}

export function dueByText(dueAt: Date): string {
  return `By the end of ${formatDayIst(lastDayToDecide(dueAt))}`;
}

export function stepDaysText(days: number): string {
  return `${workingDaysText(days)} to decide, once it starts waiting.`;
}

export function waitingText(stepTitle: string, waitingFor: string): string {
  return `${stepTitle} step, waiting for ${waitingFor}`;
}

// Requests of one kind read alike in a list, so a request's link is named after its record, who asked and when as well.
export function requestLinkName(
  kindTitle: string,
  subjectName: string | undefined,
  askedBy: string,
  submittedAt: Date,
): string {
  const record = subjectName === undefined ? "" : `: ${subjectName}`;
  return `${kindTitle}${record}, asked by ${askedBy} on ${formatDateTimeIst(submittedAt)}`;
}

export function closedOnText(closedAt: Date): string {
  return `on ${formatDateTimeIst(closedAt)}`;
}

export function workingDaysText(days: number): string {
  return days === 1 ? "1 working day" : `${indianCount.format(days)} working days`;
}

export function waitingCountText({ waitingForMe, lateForMe }: RequestCounts): string | undefined {
  if (waitingForMe <= 0) return undefined;
  if (waitingForMe === 1) {
    return lateForMe > 0
      ? "1 request waits for your decision, and it is late."
      : "1 request waits for your decision.";
  }
  const waiting = `${indianCount.format(waitingForMe)} requests wait for your decision`;
  if (lateForMe <= 0) return `${waiting}.`;
  if (lateForMe >= waitingForMe) return `${waiting}, and all of them are late.`;
  return `${waiting}, ${indianCount.format(lateForMe)} of them late.`;
}

export function mineCountText({ mineWaiting }: RequestCounts): string | undefined {
  if (mineWaiting <= 0) return undefined;
  return mineWaiting === 1
    ? "1 of your requests still waits for a decision."
    : `${indianCount.format(mineWaiting)} of your requests still wait for a decision.`;
}

export function stepSentence(step: StepFacts, requestState: ApprovalState): string {
  const { state, approver, startedAt } = step;
  switch (state) {
    case "pending":
      return `Waits for ${approver} once the steps before it are approved.`;
    case "waiting":
      return startedAt === undefined
        ? `Waiting for ${approver}.`
        : `Waiting for ${approver} since ${formatDateTimeIst(startedAt)}.`;
    case "closed":
      if (requestState !== "withdrawn") return "Not decided: an earlier step closed the request.";
      return startedAt === undefined
        ? "Not decided: the request was withdrawn before it reached this step."
        : `Not decided: the request was withdrawn while this step waited for ${approver}.`;
    default:
      return decisionSentence(step);
  }
}

export function stepCommentLabel(state: ApprovalStepState): string {
  return commentLabels[state];
}

function decisionSentence({ state, approver, decidedBy, decidedAt, inPlaceOfApprover }: StepFacts): string {
  const verb = decisionVerbs[state] ?? stepStateLabel(state);
  const decider = decidedBy === undefined ? "" : ` by ${decidedBy}`;
  const when = decidedAt === undefined ? "" : ` on ${formatDateTimeIst(decidedAt)}`;
  const inPlace = inPlaceOfApprover ? `, in place of ${approver}` : "";
  return `${verb}${decider}${when}${inPlace}.`;
}
