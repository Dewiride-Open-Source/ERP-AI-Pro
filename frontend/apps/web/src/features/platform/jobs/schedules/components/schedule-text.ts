import type { JobScheduleResponse } from "@dewiride/erp-api-client";

import { formatDateTimeIst } from "@/shared/format/dates";

export const onRequest = "Only when something in the ERP asks for it";

export const noScheduleSet = "Not set";

export const notScheduled = "Not scheduled";

export const notRunYet = "Not run yet";

export const scheduleProblemTitle = "The ERP does not run this job on its own now";

export const scheduleProblemAdvice =
  "Once its schedule setting is corrected, the ERP runs it on its own again.";

// A schedule's minutes and hours are India time, which the setting itself does not say.
export function scheduleText(schedule: JobScheduleResponse | null | undefined): string {
  if (schedule === null || schedule === undefined) return onRequest;
  const expression = schedule.expression?.trim() ?? "";
  return expression === "" ? noScheduleSet : `${expression} (India time)`;
}

export function nextRunText(schedule: JobScheduleResponse): string {
  const next = schedule.nextOccurrenceAt;
  return next instanceof Date ? formatDateTimeIst(next) : notScheduled;
}

export function lastRunText(schedule: JobScheduleResponse): string {
  const last = schedule.lastOccurrenceAt;
  return last instanceof Date ? formatDateTimeIst(last) : notRunYet;
}

export function scheduleProblem(schedule: JobScheduleResponse): string | undefined {
  const problem = schedule.problem?.trim() ?? "";
  return problem === "" ? undefined : problem;
}

export function attemptsText(maxAttempts: number | null | undefined): string {
  if (typeof maxAttempts !== "number" || maxAttempts < 1) return "Not known";
  return maxAttempts === 1 ? "Once" : `Up to ${maxAttempts} times`;
}
