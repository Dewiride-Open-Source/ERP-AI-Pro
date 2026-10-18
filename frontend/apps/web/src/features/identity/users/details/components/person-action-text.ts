import type { PersonDeactivationReason, PersonStatus } from "@dewiride/erp-api-client";

import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import type { FailedFormState, InvalidFormState } from "@/shared/forms/state/form-state";

import { countLabel } from "../../organisation/components/disclosure-tree.ts";
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

export type TeamHandoverMode = "deactivate" | "hand-over";

export interface TeamHandover {
  readonly title: string;
  readonly description: string;
  readonly team: string;
  readonly confirm: string;
  readonly cancel: string;
  readonly done: string;
}

export const ownRecordNote = "This is your own record. Another administrator can deactivate or delete it.";

export const personAlreadyDeleted = "It had already been deleted.";

export const pageOutOfDate = "This page is out of date. Reload the page, then try again.";

export const ownRecordInactive =
  "Your own record in the ERP was deactivated or deleted meanwhile, so nothing was changed.";

export const handOverTeamAction = "Hand over their team";

export const ownStatusRefused = "You cannot change the status of your own record. Another administrator can.";

export const statusProblemMessages: ProblemMessages = {
  "user.own-status": ownStatusRefused,
  "concurrency.conflict":
    "Someone else changed this record after the page was loaded. Reload the page, then try again.",
  "user.not-found": personGone,
  "user.team-handover-required":
    "People report to this person today or from a later date. Reload the page to choose who they report to, then deactivate them again.",
  "user.actor-inactive": ownRecordInactive,
};

export const deletionProblemMessages: ProblemMessages = {
  "user.own-deletion": "You cannot delete your own record. Another administrator can.",
  "user.manages-people":
    "People report to this person today or from a later date. Record another manager for them first.",
  "concurrency.conflict": organisationBusy,
};

export function statusChange(
  status: PersonStatus,
  name: string,
  deactivatedBecause?: PersonDeactivationReason | undefined,
): StatusChange {
  if (status === "deactivated") {
    return {
      to: "active",
      action: "Reactivate",
      title: `Reactivate ${name}?`,
      description: reactivation(name, deactivatedBecause),
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

export function teamHandover(mode: TeamHandoverMode, name: string, teamSize: number): TeamHandover {
  const members = countLabel(teamSize, `person reports to ${name}`, `people report to ${name}`);
  const team = `${members} today or from a later date.`;
  if (mode === "deactivate") {
    const { title, description, confirm, cancel, done } = statusChange("active", name);
    return { title, description, team, confirm, cancel, done };
  }
  return {
    title: `Hand over the team of ${name}`,
    description: `${name} is deactivated, so the people who report to them need someone else to report to.`,
    team,
    confirm: "Hand over the team",
    cancel: "Cancel",
    done: `Handed over the team of ${name}.`,
  };
}

export function teamMembersLabel(name: string): string {
  return `People who report to ${name}`;
}

export function teamUnreadable(name: string): string {
  return `The people who report to ${name} could not be read, so their team cannot be handed over now.`;
}

export function deletion(name: string, status: PersonStatus | undefined): Deletion {
  return {
    title: `Delete ${name}?`,
    description:
      status === "deactivated"
        ? `${name} is removed from People. Being deactivated is what keeps them out, and deleting the record ends that: if they can still sign in through Microsoft Entra, their next sign-in creates a new, active record. To keep them out, keep the record. Their changes stay in the audit trail.`
        : `${name} is removed from People, and any session they have open ends at their next step. If they can still sign in through Microsoft Entra, their next sign-in creates a new record, so deactivate them instead to keep them out. Their changes stay in the audit trail.`,
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

function reactivation(name: string, deactivatedBecause: PersonDeactivationReason | undefined): string {
  switch (deactivatedBecause) {
    case "accountDisabled":
      return `${name} can sign in again once their Microsoft account is enabled. While it stays disabled, the next check of Microsoft accounts deactivates them again.`;
    case "accountDeleted":
      return `Their Microsoft account was deleted, so ${name} cannot sign in with it, and the next check of Microsoft accounts deactivates them again.`;
    case "administrator":
    case undefined:
      return `${name} can sign in again with their Microsoft account.`;
  }
}
