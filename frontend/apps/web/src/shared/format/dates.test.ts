import assert from "node:assert/strict";
import { test } from "node:test";

import { formatDateTimeIst, formatDayIst, formatTimeIst, istDateKey } from "./dates.ts";

test("formatTimeIst_Instant_ShowsTheHourAndMinuteInIndiaWithTheZone", () => {
  assert.equal(formatTimeIst(new Date("2026-10-08T09:30:00Z")), "3:00 pm IST");
  assert.equal(formatTimeIst(new Date("2026-10-08T12:14:59Z")), "5:44 pm IST");
});

test("formatTimeIst_AroundMidnightAndNoonInIndia_UsesTheTwelveHourClock", () => {
  assert.equal(formatTimeIst(new Date("2026-10-08T18:30:00Z")), "12:00 am IST");
  assert.equal(formatTimeIst(new Date("2026-10-08T06:30:00Z")), "12:00 pm IST");
  assert.equal(formatTimeIst(new Date("2026-10-08T21:59:00Z")), "3:29 am IST");
});

test("formatDateTimeIst_Instant_ShowsTheDateAndTimeInIndia", () => {
  assert.equal(formatDateTimeIst(new Date("2026-10-08T09:30:00Z")), "8 Oct 2026, 3:00 pm");
});

test("formatDayIst_Instant_ShowsTheWeekdayAndDateInIndia", () => {
  assert.equal(formatDayIst(new Date("2026-10-16T09:30:00Z")), "Friday, 16 October 2026");
});

test("formatDayIst_AroundMidnightInIndia_ShowsTheIndianDay", () => {
  assert.equal(formatDayIst(new Date("2026-10-16T18:29:59.999Z")), "Friday, 16 October 2026");
  assert.equal(formatDayIst(new Date("2026-10-16T18:30:00Z")), "Saturday, 17 October 2026");
});

test("istDateKey_Instant_IsTheIndianCalendarDate", () => {
  assert.equal(istDateKey(new Date("2026-10-16T09:30:00Z")), "2026-10-16");
});

test("istDateKey_AroundMidnightInIndia_ChangesAtIndianMidnight", () => {
  assert.equal(istDateKey(new Date("2026-10-16T18:29:59.999Z")), "2026-10-16");
  assert.equal(istDateKey(new Date("2026-10-16T18:30:00Z")), "2026-10-17");
});

test("istDateKey_LastInstantsOfAMonthAndAYear_RollOverInIndia", () => {
  assert.equal(istDateKey(new Date("2028-02-28T18:30:00Z")), "2028-02-29");
  assert.equal(istDateKey(new Date("2027-03-31T18:30:00Z")), "2027-04-01");
  assert.equal(istDateKey(new Date("2026-12-31T18:29:59.999Z")), "2026-12-31");
  assert.equal(istDateKey(new Date("2026-12-31T18:30:00Z")), "2027-01-01");
});
