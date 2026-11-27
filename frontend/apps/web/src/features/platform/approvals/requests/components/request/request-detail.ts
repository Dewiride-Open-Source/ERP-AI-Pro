import type {
  ApprovalRequestResponse,
  ApprovalState,
  ApprovalStepResponse,
  ApprovalStepState,
  EarlierApprovalRequestResponse,
} from "@dewiride/erp-api-client";

import { nonEmpty, requestRow, waitingStepOf, type RequestRow } from "../request-rows.ts";
import { approverText, personText } from "../request-text.ts";

export interface RequestStepItem {
  readonly order: number;
  readonly title: string;
  readonly state: ApprovalStepState;
  readonly approver: string;
  readonly workingDays: number;
  readonly startedAt: Date | undefined;
  readonly dueAt: Date | undefined;
  readonly late: boolean;
  readonly decidedBy: string | undefined;
  readonly decidedAt: Date | undefined;
  readonly inPlaceOfApprover: boolean;
  readonly comment: string | undefined;
}

export interface EarlierRequestItem {
  readonly id: string;
  readonly state: ApprovalState;
  readonly submittedAt: Date;
  readonly closedAt: Date | undefined;
}

// What the reader may do is the API's to say: it knows who the request waits for, who asked and who may decide in place.
export interface RequestDetail {
  readonly request: RequestRow;
  readonly version: string;
  readonly steps: readonly RequestStepItem[];
  readonly earlier: readonly EarlierRequestItem[];
  readonly canDecide: boolean;
  readonly decidesInPlace: boolean;
  readonly canWithdraw: boolean;
  readonly waitingApprover: string | undefined;
}

// The plain values a decision or a withdrawal is sent from and worded with, which cross to the browser.
export interface RequestActionSubject {
  readonly id: string;
  readonly version: string;
  readonly askedBy: string;
  readonly waitingStepTitle: string | undefined;
  readonly nextStepTitle: string | undefined;
  readonly inPlaceOf: string | undefined;
}

export function requestDetailOf(
  response: ApprovalRequestResponse,
  viewer: string | undefined,
): RequestDetail | undefined {
  const request = requestRow(response, viewer);
  const version = nonEmpty(response.version);
  const steps = (response.steps ?? []).flatMap((step) => stepItemOf(step, viewer) ?? []);
  if (request === undefined || version === undefined || steps.length === 0) return undefined;
  const waiting = waitingStepOf(response);
  return {
    request,
    version,
    steps: steps.sort((left, right) => left.order - right.order),
    earlier: (response.earlierRequests ?? []).flatMap((earlier) => earlierItemOf(earlier) ?? []),
    canDecide: response.canDecide === true,
    decidesInPlace: response.decidesInPlace === true,
    canWithdraw: response.canWithdraw === true,
    waitingApprover: waiting === undefined ? undefined : approverText(waiting, viewer),
  };
}

export function actionSubjectOf(detail: RequestDetail): RequestActionSubject {
  const waiting = detail.steps.find((step) => step.state === "waiting");
  const next = waiting === undefined ? undefined : detail.steps.find((step) => step.order > waiting.order);
  return {
    id: detail.request.id,
    version: detail.version,
    askedBy: detail.request.askedBy,
    waitingStepTitle: waiting?.title,
    nextStepTitle: next?.title,
    inPlaceOf: detail.decidesInPlace ? detail.waitingApprover : undefined,
  };
}

function stepItemOf(step: ApprovalStepResponse, viewer: string | undefined): RequestStepItem | undefined {
  const { order, state, approverKind, workingDays } = step;
  const title = nonEmpty(step.title);
  if (
    typeof order !== "number" ||
    title === undefined ||
    !state ||
    !approverKind ||
    typeof workingDays !== "number"
  ) {
    return undefined;
  }
  const decidedById = step.decidedById ?? undefined;
  return {
    order,
    title,
    state,
    approver: approverText(
      {
        kind: approverKind,
        id: step.approverId ?? undefined,
        name: nonEmpty(step.approverName),
        role: step.approverRole ?? undefined,
      },
      viewer,
    ),
    workingDays,
    startedAt: step.startedAt instanceof Date ? step.startedAt : undefined,
    dueAt: step.dueAt instanceof Date ? step.dueAt : undefined,
    late: state === "waiting" && step.late === true,
    decidedBy:
      decidedById === undefined
        ? undefined
        : personText({ id: decidedById, name: nonEmpty(step.decidedByName) }, viewer),
    decidedAt: step.decidedAt instanceof Date ? step.decidedAt : undefined,
    inPlaceOfApprover: step.inPlaceOfApprover === true,
    comment: nonEmpty(step.comment),
  };
}

function earlierItemOf(earlier: EarlierApprovalRequestResponse): EarlierRequestItem | undefined {
  const { id, state, submittedAt } = earlier;
  if (!id || !state || !(submittedAt instanceof Date)) return undefined;
  return {
    id,
    state,
    submittedAt,
    closedAt: earlier.closedAt instanceof Date ? earlier.closedAt : undefined,
  };
}
