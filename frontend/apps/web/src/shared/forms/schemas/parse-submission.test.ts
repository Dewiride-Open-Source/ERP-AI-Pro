import assert from "node:assert/strict";
import { test } from "node:test";

import { z } from "zod";

import { formMessages } from "../errors/form-messages.ts";
import { parseSubmission } from "./parse-submission.ts";

const key = "3f2b8c1e-9d4a-4b6e-8f0a-1c2d3e4f5a6b";

const orderSchema = z.object({
  reference: z.string().trim().min(1, "Enter a reference."),
  lines: z
    .array(z.object({ amount: z.string().min(1, "Enter an amount.") }))
    .min(1, "Add at least one line."),
});

const validValues = { reference: "  PO-17  ", lines: [{ amount: "250.00" }] };

test("parseSubmission_ValidValues_ReturnsTheParsedData", async () => {
  const parsed = await parseSubmission(orderSchema, { values: validValues });

  assert.deepEqual(parsed, { ok: true, data: { reference: "PO-17", lines: [{ amount: "250.00" }] } });
});

test("parseSubmission_IdempotentSubmissionWithAUuid_ReturnsTheKeyWithTheData", async () => {
  const parsed = await parseSubmission(
    orderSchema,
    { values: validValues, idempotencyKey: key },
    { idempotent: true },
  );

  assert.deepEqual(parsed, {
    ok: true,
    data: { reference: "PO-17", lines: [{ amount: "250.00" }] },
    idempotencyKey: key,
  });
});

test("parseSubmission_IdempotentSubmissionWithoutAUsableKey_IsRefusedAsNotSent", async () => {
  for (const idempotencyKey of [undefined, "", "not-a-uuid", 42]) {
    const parsed = await parseSubmission(
      orderSchema,
      { values: validValues, idempotencyKey },
      { idempotent: true },
    );

    assert.deepEqual(
      parsed,
      { ok: false, state: { status: "failed", message: formMessages.notSent } },
      String(idempotencyKey),
    );
  }
});

test("parseSubmission_SubmissionThatIsNotAnEnvelopeOfValues_IsRefusedAsNotSent", async () => {
  for (const submission of [null, undefined, "values", {}, { values: "text" }, { values: [validValues] }]) {
    const parsed = await parseSubmission(orderSchema, submission);

    assert.deepEqual(
      parsed,
      { ok: false, state: { status: "failed", message: formMessages.notSent } },
      JSON.stringify(submission),
    );
  }
});

test("parseSubmission_InvalidValues_IsRefusedWithTheFieldPathsOfTheForm", async () => {
  const parsed = await parseSubmission(orderSchema, { values: { reference: " ", lines: [{ amount: "" }] } });

  assert.deepEqual(parsed, {
    ok: false,
    state: {
      status: "invalid",
      fieldErrors: { reference: ["Enter a reference."], "lines.0.amount": ["Enter an amount."] },
      formErrors: [],
    },
  });
});

test("parseSubmission_PlainSubmissionCarryingAKey_LeavesTheKeyOut", async () => {
  const parsed = await parseSubmission(orderSchema, { values: validValues, idempotencyKey: key });

  assert.deepEqual(Object.keys(parsed), ["ok", "data"]);
});

test("parseSubmission_AsyncRefinement_IsAwaited", async () => {
  const schema = z.object({
    reference: z.string().refine(async (value) => Promise.resolve(value !== "PO-1"), "Use a new reference."),
  });

  const parsed = await parseSubmission(schema, { values: { reference: "PO-1" } });

  assert.deepEqual(parsed, {
    ok: false,
    state: { status: "invalid", fieldErrors: { reference: ["Use a new reference."] }, formErrors: [] },
  });
});
