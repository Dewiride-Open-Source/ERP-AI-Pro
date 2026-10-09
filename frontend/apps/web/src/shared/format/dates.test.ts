import assert from "node:assert/strict";
import { test } from "node:test";

import { formatDateTimeIst, formatTimeIst } from "./dates.ts";

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
