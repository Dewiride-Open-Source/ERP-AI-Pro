import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

export type RequestAction = "approve" | "return" | "reject" | "withdraw";

export const commentMaxLength = 1000;

const versionMaxLength = 64;

const commentTooLong = "Use 1,000 characters or fewer.";

// Sending a request back says what needs to change and rejecting it says why, as the API requires; approving and withdrawing
// may give a note or a reason, or none.
const reasonRequired: Readonly<Partial<Record<RequestAction, string>>> = {
  return: "Say what needs to change.",
  reject: "Say why it is rejected.",
};

const idSchema = z.guid({ error: formMessages.pageOutOfDate });

const versionSchema = z
  .string({ error: formMessages.pageOutOfDate })
  .min(1, { error: formMessages.pageOutOfDate })
  .max(versionMaxLength, { error: formMessages.pageOutOfDate });

function commentSchema(requiredMessage: string | undefined) {
  const comment = z.string().trim().max(commentMaxLength, { error: commentTooLong });
  const checked = requiredMessage === undefined ? comment : comment.min(1, { error: requiredMessage });
  return checked.transform((text) => (text === "" ? null : text));
}

export function requestActionSchema(action: RequestAction) {
  return z.object({
    id: idSchema,
    version: versionSchema,
    comment: commentSchema(reasonRequired[action]),
  });
}

export type RequestActionValues = z.input<ReturnType<typeof requestActionSchema>>;

export type RequestActionSubmission = z.output<ReturnType<typeof requestActionSchema>>;

export function requestActionValues(id: string, version: string): RequestActionValues {
  return { id, version, comment: "" };
}

export function requiresReason(action: RequestAction): boolean {
  return reasonRequired[action] !== undefined;
}

export const notWaitingCode = "approval.not-waiting";

export const requestActionProblemMessages: ProblemMessages = {
  [notWaitingCode]:
    "This request no longer waits for a decision: it was decided or withdrawn after the page was loaded. Show the latest version of the request.",
  "concurrency.conflict":
    "Someone else acted on this request while you worked. Show the latest version of the request, then try again if it still waits.",
  "approval.not-yours":
    "This request no longer waits for your decision. Show the latest version of the request.",
  "approval.own-request": "Nobody decides a request they asked for or submitted.",
  "approval.not-yours-to-withdraw":
    "Only the person the request is for, or the person who submitted it, can withdraw it.",
  "approval.request-not-found": "This request no longer waits for you, so you can no longer see it.",
  "approval.kind-retired":
    "This version of the ERP no longer asks approval for this kind of request, so it can be neither decided nor withdrawn here.",
};

export const requestOutOfDateCodes: ReadonlySet<string> = new Set([
  notWaitingCode,
  "concurrency.conflict",
  "approval.not-yours",
]);
