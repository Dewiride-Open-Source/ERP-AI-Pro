import type { ApprovalRequestResponse } from "@dewiride/erp-api-client";

import type { RequestAction } from "../../forms/request-action.schema.ts";
import { waitingStepOf } from "../request-rows.ts";
import { approverText } from "../request-text.ts";

import type { RequestActionSubject } from "./request-detail.ts";

export interface ActionText {
  readonly button: string;
  readonly title: string;
  readonly commentLabel: string;
  readonly commentDescription: string;
}

export const actionTexts: Readonly<Record<RequestAction, ActionText>> = {
  approve: {
    button: "Approve",
    title: "Approve this request",
    commentLabel: "Note",
    commentDescription: "Optional, up to 1,000 characters. Everyone who can see the request can read it.",
  },
  return: {
    button: "Send back",
    title: "Send back this request",
    commentLabel: "What needs to change?",
    commentDescription: "Up to 1,000 characters. Everyone who can see the request can read it.",
  },
  reject: {
    button: "Reject",
    title: "Reject this request",
    commentLabel: "Why is it rejected?",
    commentDescription: "Up to 1,000 characters. Everyone who can see the request can read it.",
  },
  withdraw: {
    button: "Withdraw",
    title: "Withdraw this request",
    commentLabel: "Reason",
    commentDescription: "Optional, up to 1,000 characters. Everyone who can see the request can read it.",
  },
};

export const showLatestLabel = "Show the latest version of the request";

export function inPlaceNotice(approver: string): string {
  return `You decide in place of ${approver}. The request's history will say so.`;
}

export function actionDescription(action: RequestAction, subject: RequestActionSubject): string {
  switch (action) {
    case "approve":
      return approveDescription(subject);
    case "return":
      return `Sending it back closes the request. ${subject.askedBy} can change the record and ask again.`;
    case "reject":
      return "Rejecting it closes the request without approving it.";
    case "withdraw":
      return "Withdrawing it closes the request, and nobody decides it.";
  }
}

export function actionDoneMessage(
  action: RequestAction,
  answer: ApprovalRequestResponse,
  viewer: string | undefined,
): string {
  switch (action) {
    case "approve":
      return approvedMessage(answer, viewer);
    case "return":
      return "You sent the request back.";
    case "reject":
      return "You rejected the request.";
    case "withdraw":
      return "You withdrew the request.";
  }
}

function approveDescription({ waitingStepTitle, nextStepTitle }: RequestActionSubject): string {
  if (waitingStepTitle === undefined) return "Approving it decides the step the request waits at.";
  return nextStepTitle === undefined
    ? `Approving the ${waitingStepTitle} step approves the request.`
    : `Approving the ${waitingStepTitle} step sends the request on to its next step, ${nextStepTitle}.`;
}

// A step approved before the last starts the next one, which the answer describes; the last approves the request. Steps are
// approved in their order, so the step just approved is the approved one that comes last.
function approvedMessage(answer: ApprovalRequestResponse, viewer: string | undefined): string {
  const waiting = waitingStepOf(answer);
  if (answer.state !== "submitted" || waiting === undefined) return "You approved the request.";
  const approved = (answer.steps ?? [])
    .filter((step) => step.state === "approved" && typeof step.order === "number")
    .reduce<{ readonly order: number; readonly title: string } | undefined>((latest, step) => {
      const order = step.order ?? 0;
      const title = step.title?.trim() ?? "";
      return title !== "" && (latest === undefined || order > latest.order) ? { order, title } : latest;
    }, undefined);
  const decided =
    approved === undefined ? "You approved a step." : `You approved the ${approved.title} step.`;
  return `${decided} Next, the ${waiting.title} step waits for ${approverText(waiting, viewer)}.`;
}
