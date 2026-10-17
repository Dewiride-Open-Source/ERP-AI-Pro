import assert from "node:assert/strict";
import { test } from "node:test";

import { scrollbarWidth } from "./page-scrollbar.ts";

test("scrollbarWidth_ScrollbarOfWholeDevicePixelsAtAFractionalScale_KeepsTheFraction", () => {
  assert.equal(scrollbarWidth(1378.1817626953125, 1362.7271728515625), 15.45458984375);
});

test("scrollbarWidth_PageWithoutAScrollbar_IsZero", () => {
  assert.equal(scrollbarWidth(1280, 1280), 0);
});

test("scrollbarWidth_PageMeasuredWiderThanTheViewport_IsZero", () => {
  assert.equal(scrollbarWidth(360, 360.25), 0);
});
