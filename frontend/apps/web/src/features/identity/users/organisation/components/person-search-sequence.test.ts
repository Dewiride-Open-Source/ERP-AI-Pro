import assert from "node:assert/strict";
import { test } from "node:test";

import { personSearchSequence, typingPause, type PauseTimer } from "./person-search-sequence.ts";

function manualTimer(): {
  readonly timer: PauseTimer<number>;
  readonly advance: (milliseconds: number) => void;
} {
  let now = 0;
  let next = 0;
  const waiting = new Map<number, { readonly at: number; readonly run: () => void }>();
  return {
    timer: {
      set: (run, delay) => {
        next += 1;
        waiting.set(next, { at: now + delay, run });
        return next;
      },
      clear: (handle) => {
        if (handle !== undefined) waiting.delete(handle);
      },
    },
    advance: (milliseconds) => {
      now += milliseconds;
      for (const [handle, { at, run }] of [...waiting]) {
        if (at > now) continue;
        waiting.delete(handle);
        run();
      }
    },
  };
}

test("personSearchSequence_AnswerToASearchSentBeforeAnother_IsDropped", () => {
  const sequence = personSearchSequence(manualTimer().timer);

  const earlier = sequence.ticket();
  const later = sequence.ticket();

  assert.equal(sequence.isLatest(earlier), false);
  assert.equal(sequence.isLatest(later), true);
});

test("personSearchSequence_AnswerToTheOnlySearchSent_IsKept", () => {
  const sequence = personSearchSequence(manualTimer().timer);

  assert.equal(sequence.isLatest(sequence.ticket()), true);
});

test("personSearchSequence_Typing_SendsOnlyTheLastTextOnceTypingHasPaused", () => {
  const clock = manualTimer();
  const sequence = personSearchSequence(clock.timer);
  const sent: string[] = [];

  sequence.afterPause(() => sent.push("as"));
  clock.advance(typingPause - 1);
  assert.equal(sent.length, 0);
  sequence.afterPause(() => sent.push("asha"));
  clock.advance(typingPause - 1);
  assert.equal(sent.length, 0);
  clock.advance(1);

  assert.deepEqual(sent, ["asha"]);
});

test("personSearchSequence_NothingTypedOrSent_HasNotStarted", () => {
  const clock = manualTimer();
  const sequence = personSearchSequence(clock.timer);
  assert.equal(sequence.started(), false);

  sequence.afterPause(() => sequence.ticket());
  assert.equal(sequence.started(), true);
  clock.advance(typingPause);
  assert.equal(sequence.started(), true);
});

test("personSearchSequence_StoppedWhileTypingPauses_SendsNothing", () => {
  const clock = manualTimer();
  const sequence = personSearchSequence(clock.timer);
  const sent: string[] = [];

  sequence.afterPause(() => sent.push("asha"));
  sequence.stop();
  clock.advance(typingPause);

  assert.deepEqual(sent, []);
  assert.equal(sequence.started(), false);
});
