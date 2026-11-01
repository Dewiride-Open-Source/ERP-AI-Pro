import type {
  JobActorResponse,
  JobRunEventKind,
  JobRunEventResponse,
  JobRunStatus,
} from "@dewiride/erp-api-client";

import { formatDateTimeIst, formatDayIst, istDateKey } from "@/shared/format/dates";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import type { FailedFormState, InvalidFormState } from "@/shared/forms/state/form-state";

export interface RunState {
  readonly status: JobRunStatus;
  readonly dueAt: Date | undefined;
  readonly stopRequested: boolean;
}

export interface RunTries {
  readonly attemptCount: number;
  readonly failureCount: number;
}

export interface RunChoice extends RunState {
  readonly jobDefined: boolean;
}

export type RunActionRefusal = {
  readonly done: false;
  readonly message: string;
  readonly reference?: string | undefined;
};

export type RunActionOutcome = { readonly done: true; readonly stopRequested: boolean } | RunActionRefusal;

export interface RunQuestion {
  readonly title: string;
  readonly description: string;
  readonly confirm: string;
  readonly keep: string;
}

export interface RunEventDay {
  readonly key: string;
  readonly heading: string;
  readonly events: readonly JobRunEventResponse[];
}

const statusLabels: Readonly<Record<JobRunStatus, string>> = {
  pending: "Waiting",
  running: "Running",
  failed: "Failed",
  succeeded: "Done",
  deadLettered: "Gave up",
  cancelled: "Cancelled",
};

export const runTitle = "Job run";

export const notFinished = "Not finished";

export const notStarted = "Not started yet";

export const noError = "None";

export const runOutOfDate = "This page is out of date. Reload the page, then try again.";

export const runGone = "This run is no longer recorded. Reload the page to see the runs there are now.";

export const rerunProblemMessages: ProblemMessages = {
  "job.run-not-rerunnable":
    "This run is waiting, running or done by now, so it cannot run again. Reload the page to see where it stands.",
  "job.not-defined":
    "This version of the ERP no longer has the job of this run, so the run cannot run again.",
  "job.run-not-found": runGone,
};

export const cancelProblemMessages: ProblemMessages = {
  "job.run-finished":
    "This run has finished meanwhile, so there is nothing to cancel. Reload the page to see how it ended.",
  "concurrency.conflict":
    "This run kept changing while it was being cancelled. Reload the page, then try again.",
  "job.run-not-found": runGone,
};

export function runStatusLabel(status: JobRunStatus): string {
  return statusLabels[status];
}

export function runStatusDetail({ status, dueAt, stopRequested }: RunState): string | undefined {
  if (status === "running" && stopRequested) return "Stop requested";
  if (status === "failed" && dueAt !== undefined) return `Tries again at ${formatDateTimeIst(dueAt)}`;
  return undefined;
}

export function runStatusText(run: RunState): string {
  const label = runStatusLabel(run.status);
  const detail = runStatusDetail(run);
  return detail === undefined ? label : `${label}, ${detail.charAt(0).toLowerCase()}${detail.slice(1)}`;
}

export function actorText(actor: JobActorResponse | null | undefined): string {
  switch (actor?.kind) {
    case "system":
      return "The ERP";
    case "anonymous":
      return "Someone not signed in";
    default:
      return nonEmpty(actor?.name) ?? "Unknown person";
  }
}

export function triesText({ attemptCount, failureCount }: RunTries): string {
  if (attemptCount <= 0) return notStarted;
  const tries = `${attemptCount} ${attemptCount === 1 ? "try" : "tries"}`;
  return failureCount <= 0 ? tries : `${tries}, ${failureCount} failed`;
}

export function givesUpText(maxAttempts: number | undefined): string {
  if (maxAttempts === undefined) return "Not known: this version of the ERP does not have this job";
  return `After ${maxAttempts} failed ${maxAttempts === 1 ? "try" : "tries"}`;
}

// A run that succeeded is never run again, because that could repeat what it did, and a run whose job this version of the
// ERP does not have cannot run at all.
export function canRerun({ status, jobDefined }: RunChoice): boolean {
  return jobDefined && (status === "failed" || status === "deadLettered" || status === "cancelled");
}

// The ERP itself stops a running run that was asked to stop: at its runner's next check, or once it takes back a run whose
// runner stopped answering, so the page asks only once.
export function canCancel({ status, stopRequested }: RunChoice): boolean {
  return status === "pending" || status === "failed" || (status === "running" && !stopRequested);
}

export function rerunQuestion(jobLabel: string, maxAttempts: number | undefined): RunQuestion {
  const tries = maxAttempts === undefined ? "all its tries" : `all ${maxAttempts} of its tries`;
  return {
    title: `Run ${jobLabel} again?`,
    description: `The ERP starts this run again in a moment, with ${tries} available again. Its earlier tries stay in its history.`,
    confirm: "Run again",
    keep: "Keep it as it is",
  };
}

export function cancelQuestion(jobLabel: string, status: JobRunStatus): RunQuestion {
  if (status === "running") {
    return {
      title: `Ask this run of ${jobLabel} to stop?`,
      description:
        "The run stops within about a minute, once it notices the request. If it finishes before then, it is recorded as done.",
      confirm: "Ask it to stop",
      keep: "Keep it running",
    };
  }
  return {
    title: `Cancel this run of ${jobLabel}?`,
    description:
      status === "failed"
        ? "The run does not try again. You can run it again later."
        : "The run does not start. You can run it again later.",
    confirm: "Cancel the run",
    keep: "Keep the run",
  };
}

export function rerunDone(jobLabel: string): string {
  return `${jobLabel} will run again in a moment.`;
}

export function cancelDone(jobLabel: string, stopRequested: boolean): string {
  return stopRequested ? `Asked the run of ${jobLabel} to stop.` : `Cancelled the run of ${jobLabel}.`;
}

// An action's request is built from what the page shows, so a request the API finds malformed means the page is out of
// date.
export function refusedOutcome(state: InvalidFormState | FailedFormState): RunActionRefusal {
  return state.status === "failed"
    ? { done: false, message: state.message, reference: state.reference }
    : { done: false, message: runOutOfDate };
}

export function eventSentence(event: JobRunEventResponse): string {
  const actor = actorText(event.actor);
  const attempt = typeof event.attempt === "number" ? `Try ${event.attempt}` : "A try";
  switch (event.kind) {
    case "enqueued":
      return `${actor} asked for this run.`;
    case "started":
      return `${attempt} started.`;
    case "succeeded":
      return `${attempt} succeeded.`;
    case "failed":
      return `${attempt} failed.`;
    case "deadLettered":
      return `${attempt} failed, so the ERP gave up on this run.`;
    case "interrupted":
      return `${attempt} stopped because the ERP was shutting down, so it does not count as a failed try.`;
    case "leaseExpired":
      return `${attempt} stopped answering, so it counts as a failed try.`;
    case "cancelRequested":
      return `${actor} asked this run to stop.`;
    case "cancelled":
      return `${actor} cancelled this run.`;
    case "rerun":
      return `${actor} set this run to run again.`;
    default:
      return "The run changed.";
  }
}

export function retryText(retryAt: Date): string {
  return `It tries again at ${formatDateTimeIst(retryAt)}.`;
}

// A try's reference is the trace id its log lines carry, so it is shown once per try, where the try starts.
export function showsReference(kind: JobRunEventKind | null | undefined): boolean {
  return kind === "started";
}

// The events arrive oldest first, so the history reads in the order things happened, grouped by their day in India.
export function runEventDays(events: readonly JobRunEventResponse[]): RunEventDay[] {
  const days: { key: string; heading: string; events: JobRunEventResponse[] }[] = [];
  for (const event of events) {
    const occurredAt = event.occurredAt;
    if (!(occurredAt instanceof Date) || Number.isNaN(occurredAt.getTime())) continue;
    const key = istDateKey(occurredAt);
    const day = days.at(-1);
    if (day !== undefined && day.key === key) day.events.push(event);
    else days.push({ key, heading: formatDayIst(occurredAt), events: [event] });
  }
  return days;
}

function nonEmpty(value: string | null | undefined): string | undefined {
  const text = value?.trim() ?? "";
  return text === "" ? undefined : text;
}
