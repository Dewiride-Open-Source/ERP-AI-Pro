import assert from "node:assert/strict";
import { test } from "node:test";

import type { JobScheduleResponse } from "@dewiride/erp-api-client";

import {
  attemptsText,
  lastRunText,
  nextRunText,
  noScheduleSet,
  notRunYet,
  notScheduled,
  onRequest,
  scheduleProblem,
  scheduleText,
} from "./schedule-text.ts";

const nextOccurrenceAt = new Date("2026-11-01T19:00:00Z");

function schedule(values: Partial<JobScheduleResponse>): JobScheduleResponse {
  return {
    expression: "30 0 * * *",
    nextOccurrenceAt,
    problem: null,
    lastOccurrenceAt: null,
    lastRunId: null,
    lastRunStatus: null,
    ...values,
  };
}

test("scheduleText_ARecurringJob_ShowsItsExpressionInIndiaTime", () => {
  assert.equal(scheduleText(schedule({})), "30 0 * * * (India time)");
});

test("scheduleText_AJobWithoutASchedule_RunsOnlyWhenAsked", () => {
  assert.equal(scheduleText(null), onRequest);
  assert.equal(scheduleText(undefined), onRequest);
});

test("scheduleText_ASettingThatHoldsNoExpression_IsNotSet", () => {
  assert.equal(scheduleText(schedule({ expression: "" })), noScheduleSet);
  assert.equal(scheduleText(schedule({ expression: "  " })), noScheduleSet);
});

test("nextRunText_AValidSchedule_ShowsTheNextRunInIndiaTime", () => {
  assert.equal(nextRunText(schedule({})), "2 Nov 2026, 12:30 am");
});

test("nextRunText_AnInvalidSchedule_IsNotScheduledAndSaysWhy", () => {
  const invalid = schedule({
    expression: "30 0 * *",
    nextOccurrenceAt: null,
    problem: "The schedule has 4 fields; it needs five.",
  });

  assert.equal(nextRunText(invalid), notScheduled);
  assert.equal(scheduleProblem(invalid), "The schedule has 4 fields; it needs five.");
  assert.equal(scheduleProblem(schedule({})), undefined);
});

test("lastRunText_TheLatestOccurrence_ShowsItsTimeOrThatNoneRanYet", () => {
  assert.equal(lastRunText(schedule({})), notRunYet);
  assert.equal(
    lastRunText(schedule({ lastOccurrenceAt: new Date("2026-10-31T19:00:00Z") })),
    "1 Nov 2026, 12:30 am",
  );
});

test("attemptsText_TheJobsLimit_SaysHowOftenItTries", () => {
  assert.equal(attemptsText(1), "Once");
  assert.equal(attemptsText(6), "Up to 6 times");
  assert.equal(attemptsText(null), "Not known");
});
