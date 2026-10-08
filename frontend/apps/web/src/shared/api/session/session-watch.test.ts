import assert from "node:assert/strict";
import { test } from "node:test";

import { sessionAnswer } from "./session-watch.ts";

const now = Date.parse("2026-10-08T09:00:05.000Z");

const answer = {
  expiresAt: "2026-10-08T09:30:00+00:00",
  lifetimeEndsAt: "2026-10-08T21:00:00+00:00",
};

test("sessionAnswer_SessionTimes_ReturnsThemWithTheOffsetToTheApiClock", () => {
  assert.deepEqual(sessionAnswer({ status: 200, body: answer, date: "Thu, 08 Oct 2026 09:00:00 GMT" }, now), {
    state: "active",
    times: {
      expiresAt: Date.parse("2026-10-08T09:30:00Z"),
      lifetimeEndsAt: Date.parse("2026-10-08T21:00:00Z"),
      clockOffset: -5000,
    },
  });
});

test("sessionAnswer_NoDateHeader_AssumesTheClocksAgree", () => {
  const result = sessionAnswer({ status: 200, body: answer, date: null }, now);

  assert.equal(result.state === "active" ? result.times.clockOffset : undefined, 0);
});

test("sessionAnswer_Unauthenticated_ReportsTheSessionEnded", () => {
  assert.deepEqual(
    sessionAnswer({ status: 401, body: { code: "request.unauthenticated" }, date: null }, now),
    { state: "ended" },
  );
});

test("sessionAnswer_AnyOtherAnswer_ReportsNothingKnown", () => {
  for (const exchange of [
    { status: 0, body: undefined, date: null },
    { status: 429, body: { code: "rate-limit.exceeded" }, date: null },
    { status: 500, body: undefined, date: null },
    { status: 400, body: { code: "antiforgery.token-invalid" }, date: null },
    { status: 200, body: undefined, date: null },
    { status: 200, body: { expiresAt: "soon", lifetimeEndsAt: answer.lifetimeEndsAt }, date: null },
    { status: 200, body: { expiresAt: answer.expiresAt }, date: null },
  ]) {
    assert.deepEqual(sessionAnswer(exchange, now), { state: "unknown" }, JSON.stringify(exchange));
  }
});
