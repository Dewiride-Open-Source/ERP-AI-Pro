import assert from "node:assert/strict";
import { test } from "node:test";

import { cubicBezier, durationInSeconds, motionTiming } from "./motion-tokens.ts";

test("durationInSeconds_MillisecondsAndSeconds_AreSeconds", () => {
  assert.equal(durationInSeconds("200ms"), 0.2);
  assert.equal(durationInSeconds(" 0.01ms "), 0.00001);
  assert.equal(durationInSeconds("6s"), 6);
  assert.equal(durationInSeconds(".5s"), 0.5);
});

test("durationInSeconds_AnythingElse_IsUndefined", () => {
  for (const value of ["", "200", "fast", "-200ms", "200 ms", "1e3ms", "var(--motion-duration-fast)"]) {
    assert.equal(durationInSeconds(value), undefined, value);
  }
});

test("cubicBezier_TokenCurve_IsItsFourPoints", () => {
  assert.deepEqual(cubicBezier("cubic-bezier(0.16, 1, 0.3, 1)"), [0.16, 1, 0.3, 1]);
  assert.deepEqual(cubicBezier("cubic-bezier(0.2,0,0,1)"), [0.2, 0, 0, 1]);
  assert.deepEqual(cubicBezier(" cubic-bezier(0.4, -0.5, 0.6, 1.5) "), [0.4, -0.5, 0.6, 1.5]);
});

test("cubicBezier_CurveCssRejects_IsUndefined", () => {
  for (const value of [
    "",
    "ease-in-out",
    "cubic-bezier(0.2, 0, 0)",
    "cubic-bezier(0.2, 0, 0, 1, 1)",
    "cubic-bezier(1.2, 0, 0, 1)",
    "cubic-bezier(0.2, 0, -0.1, 1)",
    "cubic-bezier(a, 0, 0, 1)",
    "cubic-bezier(0.2, 0, , 1)",
  ]) {
    assert.equal(cubicBezier(value), undefined, value);
  }
});

test("motionTiming_BothTokensReadable_IsTheTiming", () => {
  assert.deepEqual(motionTiming("200ms", "cubic-bezier(0.16, 1, 0.3, 1)"), {
    duration: 0.2,
    ease: [0.16, 1, 0.3, 1],
  });
});

test("motionTiming_EitherTokenUnreadable_IsUndefined", () => {
  assert.equal(motionTiming("", "cubic-bezier(0.16, 1, 0.3, 1)"), undefined);
  assert.equal(motionTiming("200ms", ""), undefined);
});
