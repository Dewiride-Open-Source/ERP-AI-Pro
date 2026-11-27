import { roleGroupText, workingDaysText } from "../../requests/components/request-text.ts";

import {
  requiresApproval,
  workingDaysInForce,
  type ApprovalKindItem,
  type KindStepItem,
} from "./kind-items.ts";

export const needsApprovalLabel = "Needs approval";

export const approvalOffLabel = "Approval switched off";

export const workingDaysLabel = "Working days for each step";

export const settingDialogDescription =
  "The change applies to the requests asked for after it. A request already waiting keeps its steps and its due times.";

export const needsApprovalDescription =
  "While it is off, each record of this kind goes through at once, without a request, so nobody but the person who made it looks at it.";

export const workingDaysDescription = "From 1 to 30, Monday to Friday, each ending at midnight India time.";

export const settingSaved = "Saved the setting.";

export const showLatestSettingsLabel = "Show the latest settings";

// A manager step goes to the manager of the person who asks as their positions stood on the day they asked, passing over a
// manager who is deactivated, whose record has no Microsoft account yet (registered by work email and not signed in) or who
// submitted the request, and to the administrators when nobody is left.
export function stepRuleText(step: KindStepItem): string {
  if (step.rule === "role" && step.role !== undefined) return `Decided by ${roleGroupText(step.role)}.`;
  return "Decided by the manager of the person who asks, as on the day they ask. A manager who has left, who was added by work email and has not signed in yet, or who submitted the request is passed over for the one above, and the administrators decide when nobody is left.";
}

export function approvalText(kind: ApprovalKindItem): string {
  return requiresApproval(kind)
    ? "Each request goes through these steps, in this order."
    : "Records of this kind go through at once, without a request, so nobody but the person who made one looks at it. Once approval is switched on again, each request goes through these steps.";
}

export function workingDaysSentence(kind: ApprovalKindItem): string {
  const days = workingDaysInForce(kind);
  const forEachStep = `${workingDaysText(days)} for each step`;
  return days === kind.declaredWorkingDays
    ? `${forEachStep}.`
    : `${forEachStep}, as an administrator set it; the ERP's own number is ${kind.declaredWorkingDays}.`;
}

export function changeKindLabel(title: string): string {
  return `Change ${title}`;
}

export function settingSavedMessage(title: string, required: boolean, days: number): string {
  return required
    ? `Saved ${title}: each step of a new request has ${workingDaysText(days)}.`
    : `Saved ${title}: its records now go through without a request.`;
}
