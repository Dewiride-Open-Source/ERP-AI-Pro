import assert from "node:assert/strict";
import { test } from "node:test";

import { z } from "zod";

import { formMessages } from "../errors/form-messages.ts";
import { schemaResolver } from "../schemas/schema-resolver.ts";
import { formFailed, formInvalid, formSucceeded, idleFormState } from "../state/form-state.ts";
import {
  fieldErrorMessages,
  formAlertContent,
  formErrorAlertContent,
  formFieldId,
  hasFieldErrors,
  renderedField,
  renderedFieldErrors,
  serverErrorMessage,
  type FieldLookup,
} from "./form-feedback.ts";

const traceId = "0af7651916cd43dd8448eb211c80319c";

const resolverOptions = { fields: {}, shouldUseNativeValidation: undefined };

const everyFieldStanding: FieldLookup = {
  rendered: () => true,
  standing: () => true,
  fieldId: (path) => `supplier-${path}`,
};

const formId = "_R_1b_";

const renderedControlIds: ReadonlySet<string> = new Set([
  formFieldId(formId, "legalName"),
  formFieldId(formId, "validity.to"),
  formFieldId(formId, "agreementDocumentId"),
]);

const onlyRenderedControls: FieldLookup = {
  rendered: renderedField(formId, renderedControlIds),
  standing: () => true,
  fieldId: (path) => formFieldId(formId, path),
};

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

test("formAlertContent_ServerErrorOnAPathNoRenderedControlCarries_IsListedWithoutALinkInTheAnswersOrder", () => {
  const state = formInvalid({
    fieldErrors: {
      customer: ["The customer is blocked."],
      legalName: ["This name is already registered."],
      "orderItems.1": ["Each product can appear on one line only."],
      "validity.to": ["The agreement must run until the end of the financial year."],
      validity: ["The validity overlaps another agreement."],
    },
    formErrors: ["Check these details against the registration certificate."],
  });

  assert.deepEqual(formAlertContent(state, onlyRenderedControls)?.messages, [
    { message: "Check these details against the registration certificate." },
    { message: "The customer is blocked." },
    { message: "This name is already registered.", fieldId: "_R_1b_-legalName" },
    { message: "Each product can appear on one line only." },
    { message: "The agreement must run until the end of the financial year.", fieldId: "_R_1b_-validity-to" },
    { message: "The validity overlaps another agreement." },
  ]);
});

test("formAlertContent_UnlinkedMessageAfterEveryRenderedFieldIsFixed_StaysInTheSummary", () => {
  const state = formInvalid({
    fieldErrors: { legalName: ["Taken."], "orderItems.1": ["Each product can appear on one line only."] },
  });

  assert.deepEqual(formAlertContent(state, { ...onlyRenderedControls, standing: () => false }), {
    title: formMessages.detailsNeedAttention,
    messages: [{ message: "Each product can appear on one line only." }],
  });
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

test("formErrorAlertContent_FormLevelMessage_IsListedUnderTheAttentionTitle", () => {
  assert.deepEqual(formErrorAlertContent("Choose two different accounts."), {
    title: formMessages.detailsNeedAttention,
    messages: [{ message: "Choose two different accounts." }],
  });
});

test("formErrorAlertContent_NoMessage_ShowsNoAlert", () => {
  assert.equal(formErrorAlertContent(undefined), undefined);
  assert.equal(formErrorAlertContent(""), undefined);
});

test("formErrorAlertContent_SchemaIssueWithoutAPath_ReachesTheSummaryWithNoFieldToFocus", async () => {
  const schema = z
    .object({ from: z.string(), to: z.string() })
    .refine((value) => value.from !== value.to, "Choose two different accounts.");

  const { errors } = await schemaResolver(schema)({ from: "cash", to: "cash" }, undefined, resolverOptions);

  assert.equal(hasFieldErrors(errors), false);
  assert.deepEqual(formErrorAlertContent(errors.root?.message), {
    title: formMessages.detailsNeedAttention,
    messages: [{ message: "Choose two different accounts." }],
  });
});

test("renderedField_ControlIdsOfTheSentForm_OwnExactlyThePathsOfTheirControls", () => {
  const rendered = renderedField(formId, renderedControlIds);

  assert.equal(rendered("legalName"), true);
  assert.equal(rendered("validity.to"), true);
  assert.equal(rendered("agreementDocumentId"), true);
  assert.equal(rendered("validity"), false);
  assert.equal(rendered("customer"), false);
  assert.equal(rendered("orderItems.1"), false);
  assert.equal(rendered("legalName.description"), false);
});

test("renderedField_ControlOfAnotherForm_OwnsNoPath", () => {
  assert.equal(renderedField("_R_2c_", renderedControlIds)("legalName"), false);
});

test("renderedFieldErrors_InvalidState_JoinsTheMessagesOfRenderedPathsOnly", () => {
  const state = formInvalid({
    fieldErrors: {
      customer: ["The customer is blocked."],
      legalName: ["This name is already registered.", "Use the name on the PAN card."],
      "validity.to": ["The agreement must run until the end of the financial year."],
    },
  });

  assert.deepEqual(renderedFieldErrors(state, renderedField(formId, renderedControlIds)), [
    ["legalName", "This name is already registered. Use the name on the PAN card."],
    ["validity.to", "The agreement must run until the end of the financial year."],
  ]);
});

test("renderedFieldErrors_StateOtherThanInvalid_SetsNoFieldError", () => {
  const rendered = () => true;

  assert.deepEqual(renderedFieldErrors(idleFormState, rendered), []);
  assert.deepEqual(renderedFieldErrors(formFailed(formMessages.unreachable), rendered), []);
  assert.deepEqual(renderedFieldErrors(formSucceeded(), rendered), []);
});

test("hasFieldErrors_ErrorsOnFieldsOrOnlyOnTheForm_TellsThemApart", () => {
  assert.equal(hasFieldErrors({}), false);
  assert.equal(hasFieldErrors({ root: { type: "custom", message: "Check the form." } }), false);
  assert.equal(hasFieldErrors({ legalName: { type: "too_small" } }), true);
  assert.equal(hasFieldErrors({ root: { type: "custom" }, lines: { root: { type: "too_small" } } }), true);
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
