import assert from "node:assert/strict";
import { test } from "node:test";

import { localDateToIsoDate } from "../../lib/calendar-date.ts";

import { calendarLimits } from "./calendar-limits.ts";

function isoOf(date: Date | undefined): string | undefined {
  return date === undefined ? undefined : localDateToIsoDate(date);
}

test("calendarLimits_NoValueAndNoLimits_OpensOnTodayWithTheDefaultSpan", () => {
  const limits = calendarLimits("2026-09-27");
  assert.equal(isoOf(limits.defaultMonth), "2026-09-27");
  assert.equal(isoOf(limits.startMonth), "1926-01-01");
  assert.equal(isoOf(limits.endMonth), "2056-12-31");
  assert.deepEqual(limits.disabled, []);
});

test("calendarLimits_ShownDate_OpensOnItsMonth", () => {
  const limits = calendarLimits("2026-09-27", { shown: "2024-02-29" });
  assert.equal(isoOf(limits.defaultMonth), "2024-02-29");
});

test("calendarLimits_ShownTextThatIsNotADate_OpensOnToday", () => {
  const limits = calendarLimits("2026-09-27", { shown: "31-02-2026" });
  assert.equal(isoOf(limits.defaultMonth), "2026-09-27");
});

test("calendarLimits_TodayOutsideTheLimits_OpensOnTheNearestLimit", () => {
  assert.equal(isoOf(calendarLimits("2026-09-27", { min: "2027-04-01" }).defaultMonth), "2027-04-01");
  assert.equal(isoOf(calendarLimits("2026-09-27", { max: "2026-03-31" }).defaultMonth), "2026-03-31");
});

test("calendarLimits_MinAndMax_DisableTheDaysOutsideThemAndBoundTheNavigation", () => {
  const limits = calendarLimits("2026-09-27", { min: "2026-04-01", max: "2027-03-31" });
  assert.equal(isoOf(limits.startMonth), "2026-04-01");
  assert.equal(isoOf(limits.endMonth), "2027-03-31");
  assert.equal(limits.disabled.length, 2);
  const [before, after] = limits.disabled;
  assert.ok(before && typeof before === "object" && "before" in before);
  assert.equal(isoOf(before.before), "2026-04-01");
  assert.ok(after && typeof after === "object" && "after" in after);
  assert.equal(isoOf(after.after), "2027-03-31");
});

test("calendarLimits_LoneLimitBeyondTheDefaultSpan_NavigatesWithinItsYear", () => {
  const future = calendarLimits("2026-09-27", { min: "2060-04-01" });
  assert.equal(isoOf(future.defaultMonth), "2060-04-01");
  assert.equal(isoOf(future.startMonth), "2060-04-01");
  assert.equal(isoOf(future.endMonth), "2060-12-31");
  const past = calendarLimits("2026-09-27", { max: "1900-03-31" });
  assert.equal(isoOf(past.defaultMonth), "1900-03-31");
  assert.equal(isoOf(past.startMonth), "1900-01-01");
  assert.equal(isoOf(past.endMonth), "1900-03-31");
});
