import type { JobRunResponse, JobRunStatus } from "@dewiride/erp-api-client";

import { actorText } from "./run-text.ts";

// A row crosses from the server to the browser, so it holds plain values only, and the words the page shows for its job and
// for who asked for it. A run whose job this version of the ERP does not have keeps the job's name as its label.
export interface RunRow {
  readonly id: string;
  readonly jobName: string;
  readonly jobLabel: string;
  readonly jobDefined: boolean;
  readonly key: string;
  readonly status: JobRunStatus;
  readonly createdAt: Date | undefined;
  readonly dueAt: Date | undefined;
  readonly startedAt: Date | undefined;
  readonly finishedAt: Date | undefined;
  readonly requestedBy: string;
  readonly attemptCount: number;
  readonly failureCount: number;
  readonly maxAttempts: number | undefined;
  readonly stopRequested: boolean;
  readonly lastErrorCode: string | undefined;
  readonly lastError: string | undefined;
}

export function runRow(run: JobRunResponse): RunRow | undefined {
  const { id, jobName, status } = run;
  if (!id || !jobName || !status) return undefined;
  const title = nonEmpty(run.jobTitle);
  return {
    id,
    jobName,
    jobLabel: title ?? jobName,
    jobDefined: title !== undefined,
    key: run.idempotencyKey ?? "",
    status,
    createdAt: run.createdAt ?? undefined,
    dueAt: run.dueAt ?? undefined,
    startedAt: run.startedAt ?? undefined,
    finishedAt: run.finishedAt ?? undefined,
    requestedBy: actorText(run.requestedBy),
    attemptCount: run.attemptCount ?? 0,
    failureCount: run.failureCount ?? 0,
    maxAttempts: run.maxAttempts ?? undefined,
    stopRequested: status === "running" && run.cancelRequestedAt instanceof Date,
    lastErrorCode: nonEmpty(run.lastErrorCode),
    lastError: nonEmpty(run.lastErrorMessage),
  };
}

export function runRows(runs: readonly JobRunResponse[]): RunRow[] {
  return runs.flatMap((run) => {
    const row = runRow(run);
    return row === undefined ? [] : [row];
  });
}

function nonEmpty(value: string | null | undefined): string | undefined {
  const text = value?.trim() ?? "";
  return text === "" ? undefined : text;
}
