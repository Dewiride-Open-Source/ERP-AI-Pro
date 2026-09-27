import assert from "node:assert/strict";
import { test } from "node:test";

import { formFailed, formInvalid, formSucceeded, idleFormState, type FormState } from "./form-state.ts";
import { keepsIdempotencyKey } from "./idempotency-key.ts";

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
