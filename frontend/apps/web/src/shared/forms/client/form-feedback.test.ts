import assert from "node:assert/strict";
import { test } from "node:test";

import { formMessages } from "../errors/form-messages.ts";
import { formFailed, formInvalid, formSucceeded, idleFormState } from "../state/form-state.ts";
import {
  fieldErrorMessages,
  formAlertContent,
  formFieldId,
  serverErrorMessage,
  type FieldLookup,
} from "./form-feedback.ts";

const traceId = "0af7651916cd43dd8448eb211c80319c";

const everyFieldStanding: FieldLookup = { standing: () => true, fieldId: (path) => `supplier-${path}` };

test("formAlertContent_InvalidState_ListsFormErrorsThenFieldErrorsLinkedToTheirFields", () => {
  const state = formInvalid({
    fieldErrors: {
      legalName: ["This name is already registered."],
      "lines.0.amount": ["Enter an amount.", "Use figures."],
    },
    formErrors: ["The supplier is blocked."],
  });

  assert.deepEqual(formAlertContent(state, everyFieldStanding), {
    title: formMessages.detailsNeedAttention,
    messages: [
      { message: "The supplier is blocked." },
      { message: "This name is already registered.", fieldId: "supplier-legalName" },
      { message: "Enter an amount.", fieldId: "supplier-lines.0.amount" },
      { message: "Use figures.", fieldId: "supplier-lines.0.amount" },
    ],
  });
});

test("formAlertContent_FieldErrorThePersonHasFixed_IsLeftOut", () => {
  const state = formInvalid({ fieldErrors: { legalName: ["Taken."], gstin: ["Wrong State."] } });

  const content = formAlertContent(state, { ...everyFieldStanding, standing: (path) => path === "gstin" });

  assert.deepEqual(content?.messages, [{ message: "Wrong State.", fieldId: "supplier-gstin" }]);
});

test("formAlertContent_EveryFieldErrorFixedAndNoFormError_ShowsNoAlert", () => {
  const state = formInvalid({ fieldErrors: { legalName: ["Taken."] } });

  assert.equal(formAlertContent(state, { ...everyFieldStanding, standing: () => false }), undefined);
});

test("formAlertContent_FailedState_IsTitledWithItsMessageAndCarriesTheReference", () => {
  assert.deepEqual(
    formAlertContent(formFailed(formMessages.serverFailed, { reference: traceId }), everyFieldStanding),
    {
      title: formMessages.serverFailed,
      messages: [],
      reference: traceId,
    },
  );
  assert.deepEqual(formAlertContent(formFailed(formMessages.unreachable), everyFieldStanding), {
    title: formMessages.unreachable,
    messages: [],
  });
});

test("formAlertContent_IdleOrSucceededState_ShowsNoAlert", () => {
  assert.equal(formAlertContent(idleFormState, everyFieldStanding), undefined);
  assert.equal(formAlertContent(formSucceeded("Saved."), everyFieldStanding), undefined);
});

test("serverErrorMessage_EachState_IsTheFormLevelMessageOrNothing", () => {
  assert.equal(serverErrorMessage(formInvalid({ fieldErrors: {} })), formMessages.detailsNeedAttention);
  assert.equal(serverErrorMessage(formFailed("This could not be saved.")), "This could not be saved.");
  assert.equal(serverErrorMessage(idleFormState), undefined);
  assert.equal(serverErrorMessage(formSucceeded()), undefined);
});

test("fieldErrorMessages_ErrorWithAMessage_IsThatMessageAlone", () => {
  assert.deepEqual(fieldErrorMessages({ type: "server", message: "This name is already registered." }), [
    "This name is already registered.",
  ]);
});

test("fieldErrorMessages_ListErrorKeptAtItsRoot_IsTheRootMessage", () => {
  assert.deepEqual(
    fieldErrorMessages({ type: "", root: { type: "too_small", message: "Add at least one line." } }),
    ["Add at least one line."],
  );
});

test("fieldErrorMessages_NoErrorOrNoMessage_IsUndefined", () => {
  assert.equal(fieldErrorMessages(undefined), undefined);
  assert.equal(fieldErrorMessages({ type: "server" }), undefined);
  assert.equal(fieldErrorMessages({ type: "server", message: "" }), undefined);
});

test("formFieldId_FieldPath_IsAnIdUsableInSelectorsAndLinks", () => {
  assert.equal(formFieldId("_R_1b_", "legalName"), "_R_1b_-legalName");
  assert.equal(formFieldId("_R_1b_", "lines.0.amount"), "_R_1b_-lines-0-amount");
  assert.equal(formFieldId(":r1:", "validity.to"), "-r1--validity-to");
});
