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

export interface NamedRun {
  readonly jobLabel: string;
  readonly createdAt: Date | undefined;
  readonly key: string;
}

export interface RunSubject extends NamedRun, RunChoice {
  readonly maxAttempts: number | undefined;
}

export interface RunLastError {
  readonly lastErrorCode: string | undefined;
  readonly lastError: string | undefined;
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

export const stillRunning =
  "This run is still running, so it cannot be cancelled yet. Try again in a few minutes: once it has stopped answering, the ERP cancels it at once.";

const leaseExpiredCode = "job.lease-expired";

const timedOutCode = "job.timed-out";

const exceptionCode = "job.exception";

const unexpectedError =
  "Something unexpected went wrong; the server's log holds the details under the try's reference.";

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

// The ERP itself stops a running run of a job it has once the run is asked to stop: at its runner's next check, or once it
// takes back a run whose runner stopped answering, so the page asks only once. No runner of this version takes back a run of
// a job it does not have, so the page asks again for such a run, which the ERP cancels at once when it has stopped answering.
export function canCancel({ status, stopRequested, jobDefined }: RunChoice): boolean {
  return (
    status === "pending" || status === "failed" || (status === "running" && (!stopRequested || !jobDefined))
  );
}

export function runName({ createdAt, key }: Pick<NamedRun, "createdAt" | "key">): string {
  return createdAt === undefined ? `key ${key}` : `created ${formatDateTimeIst(createdAt)}`;
}

export function runLinkName(run: NamedRun): string {
  return `${run.jobLabel}, ${runName(run)}`;
}

export function runActionName(action: string, run: NamedRun): string {
  return `${action}: ${runLinkName(run)}`;
}

export function rerunQuestion(run: RunSubject): RunQuestion {
  const tries = run.maxAttempts === undefined ? "all its tries" : `all ${run.maxAttempts} of its tries`;
  return {
    title: `Run ${run.jobLabel} again?`,
    description: `This run, ${runName(run)}, starts again in a moment, with ${tries} available again. Its earlier tries stay in its history.`,
    confirm: "Run again",
    keep: "Keep it as it is",
  };
}

export function cancelQuestion(run: RunSubject): RunQuestion {
  const { jobLabel, status, stopRequested, jobDefined } = run;
  const name = runName(run);
  if (status === "running" && jobDefined) {
    return {
      title: `Ask this run of ${jobLabel} to stop?`,
      description: `This run, ${name}, stops within about a minute, once it notices the request. If it finishes before then, it is recorded as done.`,
      confirm: "Ask it to stop",
      keep: "Keep it running",
    };
  }
  if (status === "running") {
    const notHere =
      "If it has stopped answering, it is cancelled at once; otherwise it stops only once the version of the ERP that started it notices the request.";
    return stopRequested
      ? {
          title: `Cancel this run of ${jobLabel}?`,
          description: `This run, ${name}, was asked to stop, but it belongs to a job this version of the ERP does not have. ${notHere}`,
          confirm: "Cancel the run",
          keep: "Keep it running",
        }
      : {
          title: `Ask this run of ${jobLabel} to stop?`,
          description: `This run, ${name}, belongs to a job this version of the ERP does not have. ${notHere}`,
          confirm: "Ask it to stop",
          keep: "Keep it running",
        };
  }
  const ending = status === "failed" ? "does not try again" : "does not start";
  return {
    title: `Cancel this run of ${jobLabel}?`,
    description: jobDefined
      ? `This run, ${name}, ${ending}. You can run it again later.`
      : `This run, ${name}, ${ending}. This version of the ERP does not have its job, so it cannot run it again.`,
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

export function cancelOutcome(outcome: RunActionOutcome, askedToStop: boolean): RunActionOutcome {
  return outcome.done && outcome.stopRequested && askedToStop
    ? { done: false, message: stillRunning }
    : outcome;
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

export function eventErrorText({ kind, errorCode, errorMessage }: JobRunEventResponse): string | undefined {
  switch (errorCode) {
    case leaseExpiredCode:
      if (kind === "leaseExpired") return undefined;
      return kind === "cancelled" ? "The try had stopped answering." : "The try stopped answering.";
    case timedOutCode:
      return "The try ran longer than its time limit, so the ERP stopped it.";
    case exceptionCode:
      return unexpectedError;
    default:
      return nonEmpty(errorMessage);
  }
}

export function lastErrorText({ lastErrorCode, lastError }: RunLastError): string {
  switch (lastErrorCode) {
    case leaseExpiredCode:
      return "A try stopped answering, so it counted as a failed try.";
    case timedOutCode:
      return "A try ran longer than its time limit, so the ERP stopped it.";
    case exceptionCode:
      return unexpectedError;
    default:
      return nonEmpty(lastError) ?? noError;
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
