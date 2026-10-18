import assert from "node:assert/strict";
import { test } from "node:test";

import { apiDateOf, isoDateOf } from "./date-only.ts";

test("isoDateOf_DateOfTheApi_WritesItAsYearMonthDay", () => {
  assert.equal(isoDateOf({ year: 2026, month: 4, day: 1, toString: () => "2026-04-01" }), "2026-04-01");
  assert.equal(isoDateOf({ year: 1, month: 12, day: 31, toString: () => "0001-12-31" }), "0001-12-31");
});

test("isoDateOf_NoDate_ReturnsUndefined", () => {
  assert.equal(isoDateOf(null), undefined);
  assert.equal(isoDateOf(undefined), undefined);
});

test("apiDateOf_IsoDate_ReturnsItsPartsAndSerialisesAsTheSameText", () => {
  const date = apiDateOf("2026-10-17");

  assert.deepEqual([date.year, date.month, date.day], [2026, 10, 17]);
  assert.equal(date.toString(), "2026-10-17");
  assert.equal(isoDateOf(date), "2026-10-17");
});

test("apiDateOf_TextThatIsNotYearMonthDay_Throws", () => {
  for (const text of ["17-10-2026", "2026-1-7", "2026-10-17T00:00:00Z", ""]) {
    assert.throws(() => apiDateOf(text), RangeError, text);
  }
});
