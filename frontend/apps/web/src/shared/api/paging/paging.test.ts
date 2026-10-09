import assert from "node:assert/strict";
import { test } from "node:test";

import { lastRequestablePage, readPageParameter } from "./paging.ts";

test("lastRequestablePage_PageSize_MatchesTheApiLimit", () => {
  assert.equal(lastRequestablePage(1), 2_147_483_647);
  assert.equal(lastRequestablePage(20), 107_374_183);
  assert.equal(lastRequestablePage(200), 10_737_419);
});

test("readPageParameter_Absent_IsTheFirstPage", () => {
  assert.equal(readPageParameter(undefined, 20), 1);
});

test("readPageParameter_PositiveWholeNumber_IsThatPage", () => {
  assert.equal(readPageParameter("1", 20), 1);
  assert.equal(readPageParameter("7", 20), 7);
  assert.equal(readPageParameter("007", 20), 7);
  assert.equal(readPageParameter("107374183", 20), 107_374_183);
});

test("readPageParameter_NotAPositiveWholeNumber_IsRefused", () => {
  for (const value of ["", "0", "00", "-1", "+2", "1.5", "1e3", "2 ", " 2", "abc", "٣"]) {
    assert.equal(readPageParameter(value, 20), undefined, `"${value}"`);
  }
});

test("readPageParameter_BeyondWhatTheApiAccepts_IsRefused", () => {
  assert.equal(readPageParameter("107374184", 20), undefined);
  assert.equal(readPageParameter("99999999999999999999", 20), undefined);
});

test("readPageParameter_RepeatedParameter_IsRefused", () => {
  assert.equal(readPageParameter(["1", "2"], 20), undefined);
});
