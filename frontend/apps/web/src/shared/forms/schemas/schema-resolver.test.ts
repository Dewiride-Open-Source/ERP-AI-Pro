import assert from "node:assert/strict";
import { test } from "node:test";

import { z } from "zod";

import { schemaResolver } from "./schema-resolver.ts";

const options = { fields: {}, shouldUseNativeValidation: undefined };

const supplierSchema = z.object({
  legalName: z.string().trim().min(1, "Enter the legal name."),
  gstin: z
    .string()
    .min(1, "Enter the GSTIN.")
    .regex(/^[0-9]{2}[0-9A-Z]{13}$/, "Enter a 15-character GSTIN."),
  lines: z
    .array(z.object({ amount: z.string().min(1, "Enter an amount.") }))
    .min(1, "Add at least one line."),
});

test("schemaResolver_ValidValues_ReturnsTheParsedOutput", async () => {
  const resolve = schemaResolver(supplierSchema);

  const result = await resolve(
    { legalName: "  Dewiride Technologies  ", gstin: "29AAAAA1303P1ZV", lines: [{ amount: "10" }] },
    undefined,
    options,
  );

  assert.deepEqual(result, {
    values: { legalName: "Dewiride Technologies", gstin: "29AAAAA1303P1ZV", lines: [{ amount: "10" }] },
    errors: {},
  });
});

test("schemaResolver_FieldWithSeveralIssues_KeepsItsFirstIssue", async () => {
  const resolve = schemaResolver(supplierSchema);

  const result = await resolve(
    { legalName: "Dewiride", gstin: "", lines: [{ amount: "1" }] },
    undefined,
    options,
  );

  assert.deepEqual(result, {
    values: {},
    errors: { gstin: { type: "too_small", message: "Enter the GSTIN." } },
  });
});

test("schemaResolver_IssueInsideAListItem_FollowsTheShapeOfTheValues", async () => {
  const resolve = schemaResolver(supplierSchema);

  const result = await resolve(
    { legalName: "Dewiride", gstin: "29AAAAA1303P1ZV", lines: [{ amount: "1" }, { amount: "" }] },
    undefined,
    options,
  );

  const lines = (result.errors as Record<string, unknown>).lines;
  assert.ok(Array.isArray(lines));
  assert.equal(lines[0], undefined);
  assert.deepEqual(lines[1], { amount: { type: "too_small", message: "Enter an amount." } });
});

test("schemaResolver_IssueOnAList_IsTheRootErrorOfThatList", async () => {
  const resolve = schemaResolver(supplierSchema);

  const result = await resolve(
    { legalName: "Dewiride", gstin: "29AAAAA1303P1ZV", lines: [] },
    undefined,
    options,
  );

  assert.deepEqual(result.errors, {
    lines: { root: { type: "too_small", message: "Add at least one line." } },
  });
});

test("schemaResolver_IssueWithoutAPath_IsTheRootErrorOfTheForm", async () => {
  const schema = z
    .object({ from: z.string(), to: z.string() })
    .refine((value) => value.from !== value.to, "Choose two different accounts.");
  const resolve = schemaResolver(schema);

  const result = await resolve({ from: "cash", to: "cash" }, undefined, options);

  assert.deepEqual(result, {
    values: {},
    errors: { root: { type: "custom", message: "Choose two different accounts." } },
  });
});

test("schemaResolver_IssuesOnAnObjectAndInsideIt_KeepsBothWhateverTheirOrder", async () => {
  const validity = z.object({ from: z.string(), to: z.string() });
  const outerFirst = z.object({ validity }).superRefine((_, context) => {
    context.addIssue({ code: "custom", path: ["validity"], message: "Check the validity." });
    context.addIssue({ code: "custom", path: ["validity", "to"], message: "End before start." });
  });
  const innerFirst = z.object({ validity }).superRefine((_, context) => {
    context.addIssue({ code: "custom", path: ["validity", "to"], message: "End before start." });
    context.addIssue({ code: "custom", path: ["validity"], message: "Check the validity." });
  });
  const values = { validity: { from: "2026-04-01", to: "2026-03-31" } };
  const expected = {
    validity: {
      type: "custom",
      message: "Check the validity.",
      to: { type: "custom", message: "End before start." },
    },
  };

  assert.deepEqual((await schemaResolver(outerFirst)(values, undefined, options)).errors, expected);
  assert.deepEqual((await schemaResolver(innerFirst)(values, undefined, options)).errors, expected);
});

test("schemaResolver_AsyncRefinement_IsAwaited", async () => {
  const schema = z.object({
    code: z.string().refine(async (value) => Promise.resolve(value !== "taken"), "Choose another code."),
  });

  const result = await schemaResolver(schema)({ code: "taken" }, undefined, options);

  assert.deepEqual(result.errors, { code: { type: "custom", message: "Choose another code." } });
});
