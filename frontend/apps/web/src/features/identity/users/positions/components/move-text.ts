import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";

import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import { personGone } from "../../registration/forms/register-person.schema.ts";
import { organisationBusy } from "../forms/record-move.schema.ts";

import type { MoveTiming } from "./move-periods.ts";

export const moveAlreadyWithdrawnCode = "position.not-found";

export const moveAlreadyWithdrawn = "It had already been withdrawn.";

export const withdrawMoveProblemMessages: ProblemMessages = {
  "position.previous-no-longer-valid":
    "Withdrawing this move would bring back the one before it, which names a department, designation or manager deleted or deactivated since; record a new move instead.",
  "position.reporting-cycle":
    "Withdrawing this move would make the person report, through their managers, to themself; record a new move instead.",
  "concurrency.conflict": organisationBusy,
  "user.not-found": personGone,
};

export function movePeriodTitle(from: string, until: string | undefined): string {
  const start = `From ${formatDisplayDate(from)}`;
  return until === undefined ? start : `${start} to ${formatDisplayDate(until)}`;
}

export function moveTimingLabel(timing: MoveTiming): string | undefined {
  switch (timing) {
    case "today":
      return "Held today";
    case "later":
      return "Starts later";
    case "earlier":
      return undefined;
  }
}

export function withdrawMoveLabel(from: string): string {
  return `Withdraw the move from ${formatDisplayDate(from)}`;
}

export function withdrawMoveDescription(from: string): string {
  return `The move from ${formatDisplayDate(from)} is removed, and the move before it applies again until the next one starts.`;
}

export function withdrawnMoveMessage(from: string): string {
  return `Withdrew the move from ${formatDisplayDate(from)}.`;
}

export function goneMoveMessage(from: string): string {
  return `The move from ${formatDisplayDate(from)} is no longer recorded.`;
}

export function recordedMoveMessage(personName: string, from: string): string {
  return `Recorded the move of ${personName} from ${formatDisplayDate(from)}.`;
}
