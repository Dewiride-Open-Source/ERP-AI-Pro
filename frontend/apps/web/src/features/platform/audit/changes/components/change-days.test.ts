import assert from "node:assert/strict";
import { test } from "node:test";

import { changeDays, undatedDayHeading, undatedDayKey } from "./change-days.ts";

interface Change {
  readonly id: string;
  readonly occurredAt?: Date | null;
}

const at = (id: string, instant: string): Change => ({ id, occurredAt: new Date(instant) });

const timeOf = (change: Change) => change.occurredAt;

function summary(changes: readonly Change[]) {
  return changeDays(changes, timeOf).map((day) => ({
    key: day.key,
    heading: day.heading,
    ids: day.changes.map((change) => change.id),
  }));
}

test("changeDays_NewestFirstAcrossIndianMidnight_StartsANewDayAtMidnightInIndia", () => {
  assert.deepEqual(
    summary([
      at("after-midnight", "2026-10-16T18:30:00Z"),
      at("before-midnight", "2026-10-16T18:29:59.999Z"),
      at("morning", "2026-10-16T03:00:00Z"),
      at("day-before", "2026-10-15T09:30:00Z"),
    ]),
    [
      { key: "2026-10-17", heading: "Saturday, 17 October 2026", ids: ["after-midnight"] },
      { key: "2026-10-16", heading: "Friday, 16 October 2026", ids: ["before-midnight", "morning"] },
      { key: "2026-10-15", heading: "Thursday, 15 October 2026", ids: ["day-before"] },
    ],
  );
});

test("changeDays_OldestFirst_KeepsThePageOrder", () => {
  assert.deepEqual(
    summary([
      at("first", "2026-03-31T18:29:59Z"),
      at("second", "2026-03-31T18:30:00Z"),
      at("third", "2026-04-01T10:00:00Z"),
    ]),
    [
      { key: "2026-03-31", heading: "Tuesday, 31 March 2026", ids: ["first"] },
      { key: "2026-04-01", heading: "Wednesday, 1 April 2026", ids: ["second", "third"] },
    ],
  );
});

test("changeDays_AChangeWithoutAValidTime_IsShownUnderItsOwnHeading", () => {
  assert.deepEqual(
    summary([
      at("dated", "2026-10-16T09:30:00Z"),
      { id: "missing" },
      { id: "null", occurredAt: null },
      { id: "invalid", occurredAt: new Date("not a time") },
    ]),
    [
      { key: "2026-10-16", heading: "Friday, 16 October 2026", ids: ["dated"] },
      { key: undatedDayKey, heading: undatedDayHeading, ids: ["missing", "null", "invalid"] },
    ],
  );
});

test("changeDays_NoChanges_HasNoDays", () => {
  assert.deepEqual(changeDays([], timeOf), []);
});
