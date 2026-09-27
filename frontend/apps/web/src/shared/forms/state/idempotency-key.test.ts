import assert from "node:assert/strict";
import { test } from "node:test";

import { formFailed, formInvalid, formSucceeded, idleFormState, type FormState } from "./form-state.ts";
import { idempotencyKeyToSend, keepsIdempotencyKey } from "./idempotency-key.ts";

const traceId = "0af7651916cd43dd8448eb211c80319c";

test("keepsIdempotencyKey_OutcomeTheApiMayNotHaveDecided_KeepsTheKey", () => {
  const undecided: readonly FormState[] = [
    formFailed("The ERP service did not respond. Try again."),
    formFailed("The server could not finish this. Try again.", { reference: traceId }),
    formFailed("Still being saved.", { code: "idempotency.in-progress", reference: traceId }),
    formFailed("This was already saved.", { code: "idempotency.replay-unavailable" }),
  ];

  for (const outcome of undecided) {
    assert.equal(keepsIdempotencyKey(outcome), true, JSON.stringify(outcome));
  }
});

test("keepsIdempotencyKey_DecidedOutcomeOrNoOutcome_ReplacesTheKey", () => {
  const decided: readonly FormState[] = [
    idleFormState,
    formInvalid({ fieldErrors: { legalName: ["This name is already registered."] } }),
    formSucceeded("Saved."),
    formFailed("The details changed since the last attempt.", { code: "idempotency.key-reused" }),
    formFailed("The supplier is blocked.", { code: "supplier.blocked", reference: traceId }),
    formFailed("You do not have permission to do this.", { code: "request.forbidden" }),
  ];

  for (const outcome of decided) {
    assert.equal(keepsIdempotencyKey(outcome), false, JSON.stringify(outcome));
  }
});

function keyMinter(): () => string {
  let minted = 0;
  return () => `key-${++minted}`;
}

test("idempotencyKeyToSend_FirstDispatch_MintsAKey", () => {
  assert.deepEqual(idempotencyKeyToSend(undefined, idleFormState, keyMinter()), {
    key: "key-1",
    after: idleFormState,
  });
});

test("idempotencyKeyToSend_SecondDispatchBeforeAnyNewAnswer_SendsTheSameKey", () => {
  const mint = keyMinter();
  const saved = formSucceeded("Saved.");

  const first = idempotencyKeyToSend(undefined, idleFormState, mint);
  assert.equal(idempotencyKeyToSend(first, idleFormState, mint).key, "key-1");

  const afterSaved = idempotencyKeyToSend(first, saved, mint);
  assert.equal(afterSaved.key, "key-2");
  assert.equal(idempotencyKeyToSend(afterSaved, saved, mint).key, "key-2");
});

test("idempotencyKeyToSend_DecidedAnswerSinceTheLastDispatch_MintsAFreshKey", () => {
  const decided: readonly FormState[] = [
    formSucceeded("Saved."),
    formInvalid({ fieldErrors: { legalName: ["This name is already registered."] } }),
    formFailed("The details changed since the last attempt.", { code: "idempotency.key-reused" }),
    formFailed("The supplier is blocked.", { code: "supplier.blocked", reference: traceId }),
  ];

  for (const answer of decided) {
    const mint = keyMinter();
    const sent = idempotencyKeyToSend(undefined, idleFormState, mint);
    assert.deepEqual(
      idempotencyKeyToSend(sent, answer, mint),
      { key: "key-2", after: answer },
      JSON.stringify(answer),
    );
  }
});

test("idempotencyKeyToSend_UndecidedAnswerSinceTheLastDispatch_KeepsTheKey", () => {
  const undecided: readonly FormState[] = [
    formFailed("The ERP service did not respond. Try again."),
    formFailed("The server could not finish this. Try again.", { reference: traceId }),
    formFailed("Still being saved.", { code: "idempotency.in-progress", reference: traceId }),
    formFailed("This was already saved.", { code: "idempotency.replay-unavailable" }),
  ];

  for (const answer of undecided) {
    const mint = keyMinter();
    const sent = idempotencyKeyToSend(undefined, idleFormState, mint);
    assert.deepEqual(
      idempotencyKeyToSend(sent, answer, mint),
      { key: "key-1", after: answer },
      JSON.stringify(answer),
    );
  }
});

test("idempotencyKeyToSend_AnotherDecidedAnswerAlikeTheLast_StartsAnotherAttempt", () => {
  const mint = keyMinter();
  const firstRefusal = formInvalid({ fieldErrors: { legalName: ["This name is already registered."] } });
  const secondRefusal = formInvalid({ fieldErrors: { legalName: ["This name is already registered."] } });

  const afterFirst = idempotencyKeyToSend(
    idempotencyKeyToSend(undefined, idleFormState, mint),
    firstRefusal,
    mint,
  );
  const afterSecond = idempotencyKeyToSend(afterFirst, secondRefusal, mint);

  assert.equal(afterFirst.key, "key-2");
  assert.equal(afterSecond.key, "key-3");
});
