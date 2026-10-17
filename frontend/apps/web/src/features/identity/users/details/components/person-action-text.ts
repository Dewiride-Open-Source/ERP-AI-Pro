import type { PersonStatus } from "@dewiride/erp-api-client";

import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import type { FailedFormState, InvalidFormState } from "@/shared/forms/state/form-state";

import { organisationBusy } from "../../positions/forms/record-move.schema.ts";
import { personGone } from "../../registration/forms/register-person.schema.ts";

export type PersonActionRefusal = {
  readonly done: false;
  readonly message: string;
  readonly reference?: string | undefined;
};

export type PersonActionOutcome =
  { readonly done: true; readonly message?: string | undefined } | PersonActionRefusal;

export interface StatusChange {
  readonly to: PersonStatus;
  readonly action: string;
  readonly title: string;
  readonly description: string;
  readonly confirm: string;
  readonly cancel: string;
  readonly done: string;
  readonly tone: "default" | "destructive";
}

export interface Deletion {
  readonly title: string;
  readonly description: string;
  readonly confirm: string;
  readonly cancel: string;
  readonly done: string;
  readonly gone: string;
}

export const ownRecordNote = "This is your own record. Another administrator can deactivate or delete it.";

export const personAlreadyDeleted = "It had already been deleted.";

export const pageOutOfDate = "This page is out of date. Reload the page, then try again.";

export const statusProblemMessages: ProblemMessages = {
  "user.own-status": "You cannot change the status of your own record. Another administrator can.",
  "concurrency.conflict":
    "Someone else changed this record after the page was loaded. Reload the page, then try again.",
  "user.not-found": personGone,
};

export const deletionProblemMessages: ProblemMessages = {
  "user.own-deletion": "You cannot delete your own record. Another administrator can.",
  "user.manages-people":
    "People report to this person today or from a later date. Record another manager for them first.",
  "concurrency.conflict": organisationBusy,
};

export function statusChange(status: PersonStatus, name: string): StatusChange {
  if (status === "deactivated") {
    return {
      to: "active",
      action: "Reactivate",
      title: `Reactivate ${name}?`,
      description: `${name} can sign in again with their Microsoft account.`,
      confirm: "Reactivate",
      cancel: "Keep them deactivated",
      done: `Reactivated ${name}.`,
      tone: "default",
    };
  }
  return {
    to: "deactivated",
    action: "Deactivate",
    title: `Deactivate ${name}?`,
    description: `${name} can no longer sign in, and any session they have open ends at their next step in the ERP. You can reactivate them later.`,
    confirm: "Deactivate",
    cancel: "Keep them active",
    done: `Deactivated ${name}.`,
    tone: "destructive",
  };
}

export function deletion(name: string): Deletion {
  return {
    title: `Delete ${name}?`,
    description: `${name} is removed from People, and any session they have open ends at their next step. If they can still sign in through Microsoft Entra, their next sign-in creates a new record, so deactivate them instead to keep them out. Their changes stay in the audit trail.`,
    confirm: "Delete",
    cancel: "Keep the record",
    done: `Deleted ${name}.`,
    gone: `${name} is no longer registered.`,
  };
}

// The record's Entra object id and the signed-in person's id are the same GUID, which nothing guarantees is written in the
// same case on both sides.
export function isOwnRecord(
  entraObjectId: string | null | undefined,
  signedInId: string | undefined,
): boolean {
  if (entraObjectId === null || entraObjectId === undefined || signedInId === undefined) return false;
  return entraObjectId.toLowerCase() === signedInId.toLowerCase();
}

// An action's request is built from what the page shows, so a request the API finds malformed means the page is out of
// date.
export function refusedOutcome(state: InvalidFormState | FailedFormState): PersonActionRefusal {
  return state.status === "failed"
    ? { done: false, message: state.message, reference: state.reference }
    : { done: false, message: pageOutOfDate };
}
