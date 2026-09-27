import assert from "node:assert/strict";
import { test } from "node:test";

import {
  calendarNavigationBounds,
  clampIsoDate,
  compareIsoDates,
  formatDisplayDate,
  isIsoDate,
  isoDateToLocalDate,
  localDateToIsoDate,
  parseDisplayDate,
  toCanonicalDate,
} from "./calendar-date.ts";

test("parseDisplayDate_DayMonthYearWithAnySeparator_ReturnsTheIsoDate", () => {
  const cases: [string, string][] = [
    ["31-03-2026", "2026-03-31"],
    ["31/03/2026", "2026-03-31"],
    ["31.03.2026", "2026-03-31"],
    ["1-4-2026", "2026-04-01"],
    ["01/4/2026", "2026-04-01"],
    ["  01-04-2026 ", "2026-04-01"],
    ["01042026", "2026-04-01"],
    ["2026-04-01", "2026-04-01"],
    ["2026-4-1", "2026-04-01"],
    ["29-02-2024", "2024-02-29"],
    ["29-02-2000", "2000-02-29"],
    ["01-01-0001", "0001-01-01"],
    ["31-12-9999", "9999-12-31"],
  ];
  for (const [text, expected] of cases) {
    assert.equal(parseDisplayDate(text), expected, text);
  }
});

test("parseDisplayDate_ImpossibleOrMalformedDate_ReturnsNull", () => {
  const cases = [
    "",
    "29-02-2026",
    "29-02-1900",
    "31-04-2026",
    "31-02-2026",
    "00-01-2026",
    "01-00-2026",
    "01-13-2026",
    "01-04-0000",
    "01-04-26",
    "01-04/2026",
    "001-04-2026",
    "20260401",
    "2026/04/01",
    "1 April 2026",
    "31-03-2026T00:00",
  ];
  for (const text of cases) {
    assert.equal(parseDisplayDate(text), null, JSON.stringify(text));
  }
});

test("isIsoDate_OnlyARealDateInTheWireForm_IsAccepted", () => {
  assert.equal(isIsoDate("2026-04-01"), true);
  assert.equal(isIsoDate("2024-02-29"), true);
  assert.equal(isIsoDate("2026-02-29"), false);
  assert.equal(isIsoDate("2026-4-1"), false);
  assert.equal(isIsoDate("01-04-2026"), false);
  assert.equal(isIsoDate("0000-01-01"), false);
  assert.equal(isIsoDate(""), false);
});

test("formatDisplayDate_IsoDate_ReturnsDayMonthYearWithHyphens", () => {
  assert.equal(formatDisplayDate("2026-04-01"), "01-04-2026");
  assert.equal(formatDisplayDate("0001-01-01"), "01-01-0001");
});

test("formatDisplayDate_TextThatIsNotAnIsoDate_IsReturnedUnchanged", () => {
  assert.equal(formatDisplayDate(""), "");
  assert.equal(formatDisplayDate("31-02-2026"), "31-02-2026");
  assert.equal(formatDisplayDate("2026-02-30"), "2026-02-30");
});

test("toCanonicalDate_TypedText_ReturnsTheIsoDateTheTrimmedTextOrEmpty", () => {
  assert.equal(toCanonicalDate("1/4/2026"), "2026-04-01");
  assert.equal(toCanonicalDate(" 31-02-2026 "), "31-02-2026");
  assert.equal(toCanonicalDate("   "), "");
  assert.equal(toCanonicalDate(""), "");
});

test("isoDateToLocalDate_IsoDate_ReturnsLocalMidnightOfThatDay", () => {
  const date = isoDateToLocalDate("2026-04-01");
  assert.ok(date);
  assert.equal(date.getFullYear(), 2026);
  assert.equal(date.getMonth(), 3);
  assert.equal(date.getDate(), 1);
  assert.equal(date.getHours(), 0);
  assert.equal(date.getMinutes(), 0);
});

test("isoDateToLocalDate_YearBelowOneHundred_KeepsTheYear", () => {
  assert.equal(isoDateToLocalDate("0099-01-05")?.getFullYear(), 99);
  assert.equal(isoDateToLocalDate("0001-01-01")?.getFullYear(), 1);
});

test("isoDateToLocalDate_TextThatIsNotAnIsoDate_ReturnsUndefined", () => {
  assert.equal(isoDateToLocalDate("01-04-2026"), undefined);
  assert.equal(isoDateToLocalDate("2026-02-30"), undefined);
});

test("localDateToIsoDate_LocalDate_ReturnsItsCalendarDay", () => {
  assert.equal(localDateToIsoDate(new Date(2026, 3, 1, 23, 59)), "2026-04-01");
  const early = new Date(0);
  early.setFullYear(42, 6, 9);
  assert.equal(localDateToIsoDate(early), "0042-07-09");
});

test("localDateToIsoDate_InvalidDate_Throws", () => {
  assert.throws(() => localDateToIsoDate(new Date(Number.NaN)), RangeError);
  assert.throws(() => localDateToIsoDate(new Date(10000, 0, 1)), RangeError);
});

test("isoDateToLocalDate_EveryDayOf2026InEveryTimeZoneOfTheRuntime_RoundTripsTheCalendarDay", () => {
  const original = process.env.TZ;
  let skippedMidnights = 0;
  try {
    for (const zone of Intl.supportedValuesOf("timeZone")) {
      process.env.TZ = zone;
      for (
        let day = new Date(Date.UTC(2026, 0, 1));
        day.getUTCFullYear() === 2026;
        day.setUTCDate(day.getUTCDate() + 1)
      ) {
        const isoDate = day.toISOString().slice(0, 10);
        const local = isoDateToLocalDate(isoDate);
        assert.ok(local, isoDate);
        assert.equal(localDateToIsoDate(local), isoDate, `${zone} ${isoDate}`);
        if (new Date(day.getUTCFullYear(), day.getUTCMonth(), day.getUTCDate()).getHours() !== 0) {
          skippedMidnights += 1;
        }
      }
    }
  } finally {
    if (original === undefined) delete process.env.TZ;
    else process.env.TZ = original;
  }
  assert.ok(skippedMidnights > 0, "no zone of the runtime skips a midnight in 2026");
});

test("compareIsoDates_TwoDates_OrdersThemChronologically", () => {
  assert.equal(compareIsoDates("2026-03-31", "2026-04-01"), -1);
  assert.equal(compareIsoDates("2027-01-01", "2026-12-31"), 1);
  assert.equal(compareIsoDates("2026-04-01", "2026-04-01"), 0);
});

test("clampIsoDate_DateOutsideTheLimits_MovesToTheNearestLimit", () => {
  assert.equal(clampIsoDate("2026-03-31", "2026-04-01", "2027-03-31"), "2026-04-01");
  assert.equal(clampIsoDate("2027-04-01", "2026-04-01", "2027-03-31"), "2027-03-31");
  assert.equal(clampIsoDate("2026-09-27", "2026-04-01", "2027-03-31"), "2026-09-27");
  assert.equal(clampIsoDate("2026-09-27"), "2026-09-27");
  assert.equal(clampIsoDate("2026-09-27", "not a date"), "2026-09-27");
});

test("calendarNavigationBounds_NoLimits_SpansAHundredYearsBackAndThirtyAhead", () => {
  assert.deepEqual(calendarNavigationBounds("2026-09-27"), { start: "1926-01-01", end: "2056-12-31" });
});

test("calendarNavigationBounds_Limits_AreTheBounds", () => {
  assert.deepEqual(calendarNavigationBounds("2026-09-27", { min: "2026-04-01", max: "2027-03-31" }), {
    start: "2026-04-01",
    end: "2027-03-31",
  });
});

test("calendarNavigationBounds_SelectedDateOutsideTheDefaultSpan_WidensIt", () => {
  assert.deepEqual(calendarNavigationBounds("2026-09-27", { selected: "1900-05-01" }), {
    start: "1900-01-01",
    end: "2056-12-31",
  });
  assert.deepEqual(calendarNavigationBounds("2026-09-27", { selected: "2099-05-01" }), {
    start: "1926-01-01",
    end: "2099-12-31",
  });
});

test("calendarNavigationBounds_LoneMinBeyondTheDefaultSpan_EndsInTheYearOfTheMin", () => {
  assert.deepEqual(calendarNavigationBounds("2026-09-27", { min: "2060-04-01" }), {
    start: "2060-04-01",
    end: "2060-12-31",
  });
  assert.deepEqual(calendarNavigationBounds("2026-09-27", { min: "2057-01-01" }), {
    start: "2057-01-01",
    end: "2057-12-31",
  });
});

test("calendarNavigationBounds_LoneMaxBeforeTheDefaultSpan_StartsInTheYearOfTheMax", () => {
  assert.deepEqual(calendarNavigationBounds("2026-09-27", { max: "1900-03-31" }), {
    start: "1900-01-01",
    end: "1900-03-31",
  });
  assert.deepEqual(calendarNavigationBounds("2026-09-27", { max: "1925-12-31" }), {
    start: "1925-01-01",
    end: "1925-12-31",
  });
});

test("calendarNavigationBounds_AnyLoneLimit_NeverStartsAfterItEnds", () => {
  for (let year = 1; year <= 9999; year += 37) {
    const limit = `${String(year).padStart(4, "0")}-06-15`;
    for (const options of [{ min: limit }, { max: limit }, { min: limit, selected: "1950-01-01" }]) {
      const bounds = calendarNavigationBounds("2026-09-27", options);
      assert.ok(
        compareIsoDates(bounds.start, bounds.end) <= 0,
        `${JSON.stringify(options)} ${JSON.stringify(bounds)}`,
      );
    }
  }
});

test("calendarNavigationBounds_SpanPastTheDateOnlyRange_StopsAtItsEnds", () => {
  assert.deepEqual(calendarNavigationBounds("0050-06-01"), { start: "0001-01-01", end: "0080-12-31" });
  assert.deepEqual(calendarNavigationBounds("9990-06-01"), { start: "9890-01-01", end: "9999-12-31" });
});
