import assert from "node:assert/strict";
import { test } from "node:test";

import { followPointer } from "./pointer-rest.ts";

test("followPointer_PositionNotSeenYet_RecordsItWithoutFollowing", () => {
  assert.deepEqual(followPointer("unseen", 132, 372), { follows: false, rest: { x: 132, y: 372 } });
});

test("followPointer_MoveAtTheRestingPosition_DoesNotFollow", () => {
  const rest = { x: 132, y: 372 };
  assert.deepEqual(followPointer(rest, 132, 372), { follows: false, rest });
});

test("followPointer_MoveAwayFromTheRestingPosition_FollowsFromThenOn", () => {
  const away = followPointer({ x: 132, y: 372 }, 133, 372);
  assert.deepEqual(away, { follows: true, rest: "moving" });
  assert.deepEqual(followPointer(away.rest, 133, 372), { follows: true, rest: "moving" });
});
