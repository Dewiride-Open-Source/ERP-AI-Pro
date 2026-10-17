import assert from "node:assert/strict";
import { test } from "node:test";

import { dayBefore, movePeriods } from "./move-periods.ts";

type Move = { readonly id: string; readonly effectiveFrom: string | undefined };

const startOf = (move: Move) => move.effectiveFrom;

function periods(moves: readonly Move[], today: string) {
  return movePeriods(moves, startOf, today).map(({ move, from, until, timing }) => ({
    id: move.id,
    from,
    until,
    timing,
  }));
}

test("movePeriods_MovesInAnyOrder_RunEachUntilTheDayBeforeTheNextNewestFirst", () => {
  assert.deepEqual(
    periods(
      [
        { id: "april", effectiveFrom: "2026-04-01" },
        { id: "january", effectiveFrom: "2027-01-01" },
        { id: "july", effectiveFrom: "2026-07-01" },
      ],
      "2026-10-17",
    ),
    [
      { id: "january", from: "2027-01-01", until: undefined, timing: "later" },
      { id: "july", from: "2026-07-01", until: "2026-12-31", timing: "today" },
      { id: "april", from: "2026-04-01", until: "2026-06-30", timing: "earlier" },
    ],
  );
});

test("movePeriods_MoveStartingToday_HoldsTodayAndEndsTheOneBefore", () => {
  assert.deepEqual(
    periods(
      [
        { id: "before", effectiveFrom: "2026-04-01" },
        { id: "today", effectiveFrom: "2026-10-17" },
      ],
      "2026-10-17",
    ),
    [
      { id: "today", from: "2026-10-17", until: undefined, timing: "today" },
      { id: "before", from: "2026-04-01", until: "2026-10-16", timing: "earlier" },
    ],
  );
});

test("movePeriods_MoveEndingToday_StillHoldsToday", () => {
  assert.deepEqual(
    periods(
      [
        { id: "now", effectiveFrom: "2026-04-01" },
        { id: "tomorrow", effectiveFrom: "2026-10-18" },
      ],
      "2026-10-17",
    ),
    [
      { id: "tomorrow", from: "2026-10-18", until: undefined, timing: "later" },
      { id: "now", from: "2026-04-01", until: "2026-10-17", timing: "today" },
    ],
  );
});

test("movePeriods_MoveWithoutARealDate_IsLeftOut", () => {
  assert.deepEqual(
    periods(
      [
        { id: "undated", effectiveFrom: undefined },
        { id: "impossible", effectiveFrom: "2026-02-30" },
        { id: "dated", effectiveFrom: "2026-04-01" },
      ],
      "2026-10-17",
    ),
    [{ id: "dated", from: "2026-04-01", until: undefined, timing: "today" }],
  );
  assert.deepEqual(periods([], "2026-10-17"), []);
});

test("dayBefore_FirstDaysOfMonthsAndYears_StepsBackAcrossThem", () => {
  assert.equal(dayBefore("2026-03-01"), "2026-02-28");
  assert.equal(dayBefore("2028-03-01"), "2028-02-29");
  assert.equal(dayBefore("2027-01-01"), "2026-12-31");
  assert.equal(dayBefore("0001-01-02"), "0001-01-01");
});
