import type {
  ApprovalRequestSummaryResponse,
  ApprovalState,
  ApproverKind,
  ErpRole,
} from "@dewiride/erp-api-client";

import {
  approverText,
  kindsUnavailableNote,
  personText,
  requestLinkName,
  sameId,
  submitterText,
  waitingText,
} from "./request-text.ts";

// A row crosses from the server to the browser, so it holds plain values only, with the words the page shows for the people
// in it; the reader is "you". A request asked for by one person and submitted by another names both.
export interface RequestRow {
  readonly id: string;
  readonly kind: string;
  readonly kindTitle: string;
  readonly subjectName: string | undefined;
  readonly linkName: string;
  readonly askedBy: string;
  readonly submittedBy: string | undefined;
  readonly submittedAt: Date;
  readonly state: ApprovalState;
  readonly waiting: string | undefined;
  readonly dueAt: Date | undefined;
  readonly late: boolean;
  readonly closedAt: Date | undefined;
}

export interface WaitingStep {
  readonly title: string;
  readonly kind: ApproverKind;
  readonly id: string | undefined;
  readonly name: string | undefined;
  readonly role: ErpRole | undefined;
}

type RequestFacts = Pick<
  ApprovalRequestSummaryResponse,
  | "id"
  | "kind"
  | "kindTitle"
  | "subjectName"
  | "requestedForId"
  | "requestedForName"
  | "submittedById"
  | "submittedByKind"
  | "submittedByName"
  | "submittedAt"
  | "state"
  | "waitingStepTitle"
  | "waitingForKind"
  | "waitingForId"
  | "waitingForName"
  | "waitingForRole"
  | "dueAt"
  | "late"
  | "closedAt"
>;

export function requestRow(request: RequestFacts, viewer: string | undefined): RequestRow | undefined {
  const { id, kind, state, submittedAt, requestedForId, submittedById } = request;
  if (!id || !kind || !state || !(submittedAt instanceof Date) || !requestedForId || !submittedById) {
    return undefined;
  }
  const step = waitingStepOf(request);
  const kindTitle = nonEmpty(request.kindTitle) ?? kind;
  const subjectName = nonEmpty(request.subjectName);
  const askedBy = { id: requestedForId, name: nonEmpty(request.requestedForName) };
  return {
    id,
    kind,
    kindTitle,
    subjectName,
    linkName: requestLinkName(kindTitle, subjectName, personText(askedBy, viewer), submittedAt),
    askedBy: personText(askedBy, viewer, true),
    submittedBy: sameId(requestedForId, submittedById)
      ? undefined
      : submitterText(
          request.submittedByKind,
          { id: submittedById, name: nonEmpty(request.submittedByName) },
          viewer,
        ),
    submittedAt,
    state,
    waiting: step === undefined ? undefined : waitingText(step.title, approverText(step, viewer)),
    dueAt: state === "submitted" && request.dueAt instanceof Date ? request.dueAt : undefined,
    late: state === "submitted" && request.late === true,
    closedAt: request.closedAt instanceof Date ? request.closedAt : undefined,
  };
}

export function kindFilterNote(kindsUnavailable: boolean, chosen: readonly string[]): string | undefined {
  return kindsUnavailable && chosen.length === 0 ? kindsUnavailableNote : undefined;
}

export function requestRows(requests: readonly RequestFacts[], viewer: string | undefined): RequestRow[] {
  return requests.flatMap((request) => {
    const row = requestRow(request, viewer);
    return row === undefined ? [] : [row];
  });
}

// The step a request waits at, as its fields describe it while the request is open; a closed request waits at none.
export function waitingStepOf(request: RequestFacts): WaitingStep | undefined {
  const title = nonEmpty(request.waitingStepTitle);
  const kind = request.waitingForKind ?? undefined;
  if (request.state !== "submitted" || title === undefined || kind === undefined) return undefined;
  return {
    title,
    kind,
    id: request.waitingForId ?? undefined,
    name: nonEmpty(request.waitingForName),
    role: request.waitingForRole ?? undefined,
  };
}

export function nonEmpty(value: string | null | undefined): string | undefined {
  const text = value?.trim() ?? "";
  return text === "" ? undefined : text;
}
