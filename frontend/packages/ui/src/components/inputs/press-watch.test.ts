import assert from "node:assert/strict";
import { getEventListeners } from "node:events";
import { test } from "node:test";

import { createPressWatch } from "./press-watch.ts";

function listenerCount(document: EventTarget): number {
  return getEventListeners(document, "mouseup").length + getEventListeners(document, "contextmenu").length;
}

function counter() {
  const calls = { count: 0 };
  const onEnd = () => {
    calls.count += 1;
  };
  return { calls, onEnd };
}

test("start_PressReleased_EndsThePressOnce", () => {
  const document = new EventTarget();
  const watch = createPressWatch();
  const { calls, onEnd } = counter();

  watch.start(document, onEnd);
  assert.equal(watch.pending, true);
  document.dispatchEvent(new Event("mouseup"));
  document.dispatchEvent(new Event("mouseup"));
  document.dispatchEvent(new Event("contextmenu"));

  assert.equal(calls.count, 1);
  assert.equal(watch.pending, false);
  assert.equal(listenerCount(document), 0);
});

test("start_ContextMenuTakesTheRelease_EndsThePress", () => {
  const document = new EventTarget();
  const watch = createPressWatch();
  const { calls, onEnd } = counter();

  watch.start(document, onEnd);
  document.dispatchEvent(new Event("contextmenu"));
  document.dispatchEvent(new Event("mouseup"));

  assert.equal(calls.count, 1);
  assert.equal(watch.pending, false);
  assert.equal(listenerCount(document), 0);
});

test("start_PressEnds_IsNoLongerPendingWhenTheEndRuns", () => {
  const document = new EventTarget();
  const watch = createPressWatch();
  const pendingAtEnd: boolean[] = [];

  watch.start(document, () => pendingAtEnd.push(watch.pending));
  document.dispatchEvent(new Event("mouseup"));

  assert.deepEqual(pendingAtEnd, [false]);
});

test("cancel_FocusLostBeforeTheRelease_NeverEndsThePress", () => {
  const document = new EventTarget();
  const watch = createPressWatch();
  const { calls, onEnd } = counter();

  watch.start(document, onEnd);
  watch.cancel();
  document.dispatchEvent(new Event("mouseup"));
  document.dispatchEvent(new Event("contextmenu"));

  assert.equal(calls.count, 0);
  assert.equal(watch.pending, false);
  assert.equal(listenerCount(document), 0);
});

test("cancel_NothingPending_LeavesTheWatchIdle", () => {
  const watch = createPressWatch();

  watch.cancel();

  assert.equal(watch.pending, false);
});

test("start_SecondPressBeforeTheFirstEnded_EndsOnlyTheSecond", () => {
  const document = new EventTarget();
  const watch = createPressWatch();
  const first = counter();
  const second = counter();

  watch.start(document, first.onEnd);
  watch.start(document, second.onEnd);
  assert.equal(listenerCount(document), 2);
  document.dispatchEvent(new Event("mouseup"));

  assert.equal(first.calls.count, 0);
  assert.equal(second.calls.count, 1);
  assert.equal(listenerCount(document), 0);
});

test("start_AfterAnEndedPress_WatchesTheNewPress", () => {
  const document = new EventTarget();
  const watch = createPressWatch();
  const first = counter();
  const second = counter();

  watch.start(document, first.onEnd);
  document.dispatchEvent(new Event("mouseup"));
  watch.start(document, second.onEnd);
  assert.equal(watch.pending, true);
  document.dispatchEvent(new Event("contextmenu"));

  assert.equal(first.calls.count, 1);
  assert.equal(second.calls.count, 1);
  assert.equal(watch.pending, false);
});
