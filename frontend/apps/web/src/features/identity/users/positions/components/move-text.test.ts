import assert from "node:assert/strict";
import { test } from "node:test";

import {
  goneMoveMessage,
  movePeriodTitle,
  moveTimingLabel,
  recordedMoveMessage,
  withdrawMoveDescription,
  withdrawMoveLabel,
  withdrawMoveProblemMessages,
  withdrawnMoveMessage,
} from "./move-text.ts";

test("movePeriodTitle_MoveWithAndWithoutAnEnd_ShowsItsDatesAsDayMonthYear", () => {
  assert.equal(movePeriodTitle("2026-04-01", "2026-06-30"), "From 01-04-2026 to 30-06-2026");
  assert.equal(movePeriodTitle("2026-07-01", undefined), "From 01-07-2026");
});

test("moveTimingLabel_EachTiming_NamesOnlyTheMovesThatAreNotHistory", () => {
  assert.equal(moveTimingLabel("today"), "Held today");
  assert.equal(moveTimingLabel("later"), "Starts later");
  assert.equal(moveTimingLabel("earlier"), undefined);
});

test("withdrawMoveText_Move_NamesItByItsDate", () => {
  assert.equal(withdrawMoveLabel("2026-07-01"), "Withdraw the move from 01-07-2026");
  assert.equal(
    withdrawMoveDescription("2026-07-01"),
    "The move from 01-07-2026 is removed, and the move before it applies again until the next one starts.",
  );
  assert.equal(withdrawnMoveMessage("2026-07-01"), "Withdrew the move from 01-07-2026.");
  assert.equal(goneMoveMessage("2026-07-01"), "The move from 01-07-2026 is no longer recorded.");
});

test("recordedMoveMessage_PersonAndDate_SaysWhoseMoveFromWhen", () => {
  assert.equal(
    recordedMoveMessage("Meera Nair", "2027-01-01"),
    "Recorded the move of Meera Nair from 01-01-2027.",
  );
});

test("withdrawMoveProblemMessages_RefusalsOfTheApi_AreWordedForThePerson", () => {
  assert.deepEqual(Object.keys(withdrawMoveProblemMessages).sort(), [
    "concurrency.conflict",
    "position.previous-no-longer-valid",
    "position.reporting-cycle",
    "user.not-found",
  ]);
  assert.equal(
    withdrawMoveProblemMessages["concurrency.conflict"],
    "Another organisation change is being saved; try again.",
  );
});
