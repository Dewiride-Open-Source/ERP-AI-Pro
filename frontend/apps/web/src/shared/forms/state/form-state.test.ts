import assert from "node:assert/strict";
import { test } from "node:test";

import { formFailed, formInvalid, formSucceeded, idleFormState, type FormState } from "./form-state.ts";

const traceId = "0af7651916cd43dd8448eb211c80319c";

test("idleFormState_Status_IsIdle", () => {
  assert.deepEqual(idleFormState, { status: "idle" });
});

test("formInvalid_FieldAndFormErrors_KeepsBoth", () => {
  const state = formInvalid({
    fieldErrors: { gstin: ["Enter the GSTIN."] },
    formErrors: ["The supplier is blocked."],
  });

  assert.deepEqual(state, {
    status: "invalid",
    fieldErrors: { gstin: ["Enter the GSTIN."] },
    formErrors: ["The supplier is blocked."],
  });
});

test("formInvalid_WithoutFormErrors_HasAnEmptyFormErrorList", () => {
  assert.deepEqual(formInvalid({ fieldErrors: { pan: ["Enter the PAN."] } }).formErrors, []);
});

test("formFailed_CodeAndReference_KeepsBoth", () => {
  assert.deepEqual(formFailed("This could not be saved.", { code: "supplier.blocked", reference: traceId }), {
    status: "failed",
    message: "This could not be saved.",
    code: "supplier.blocked",
    reference: traceId,
  });
});

test("formFailed_UndefinedCodeAndReference_LeavesBothMembersOut", () => {
  const state = formFailed("The ERP service did not respond. Try again.", {
    code: undefined,
    reference: undefined,
  });

  assert.deepEqual(Object.keys(state), ["status", "message"]);
});

test("formSucceeded_WithoutAMessage_HasNoMessageMember", () => {
  assert.deepEqual(Object.keys(formSucceeded()), ["status"]);
  assert.deepEqual(formSucceeded("Saved."), { status: "succeeded", message: "Saved." });
});

test("FormState_EveryStatus_SurvivesSerialisationUnchanged", () => {
  const states: FormState[] = [
    idleFormState,
    formInvalid({ fieldErrors: { "lines.0.amount": ["Enter an amount."] }, formErrors: ["Add a line."] }),
    formFailed("The server could not finish this. Try again.", { reference: traceId }),
    formSucceeded("Saved."),
  ];

  for (const state of states) {
    assert.deepEqual(JSON.parse(JSON.stringify(state)), state);
  }
});
