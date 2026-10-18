import assert from "node:assert/strict";
import { test } from "node:test";

import {
  addDesignationProblemMessages,
  addDesignationSchema,
  defaultAddDesignationValues,
  designationNameMaxLength,
  designationNameTaken,
  renameDesignationProblemMessages,
  renameDesignationSchema,
  renameDesignationValues,
} from "./designation.schema.ts";

const analyst = "0199a3f2-7c1e-7b4a-9d2e-000000000001";

function reported(
  schema: typeof addDesignationSchema | typeof renameDesignationSchema,
  values: unknown,
): string[] {
  const result = schema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("addDesignationSchema_StartingValues_AsksForTheName", () => {
  assert.deepEqual(reported(addDesignationSchema, defaultAddDesignationValues), [
    "name: Enter the designation's name.",
  ]);
});

test("addDesignationSchema_NameWithSpaces_TrimsIt", () => {
  assert.deepEqual(addDesignationSchema.parse({ name: "  Senior analyst " }), { name: "Senior analyst" });
});

test("addDesignationSchema_NameBlankOrOverTheLimit_IsRefusedOnTheName", () => {
  assert.deepEqual(reported(addDesignationSchema, { name: "  " }), ["name: Enter the designation's name."]);
  assert.deepEqual(reported(addDesignationSchema, { name: "x".repeat(designationNameMaxLength + 1) }), [
    `name: Use ${designationNameMaxLength} characters or fewer.`,
  ]);
  assert.deepEqual(reported(addDesignationSchema, { name: "x".repeat(designationNameMaxLength) }), []);
});

test("renameDesignationSchema_DesignationAsRead_ParsesItWithItsVersion", () => {
  const values = renameDesignationValues({ id: analyst, name: "Analyst", version: "AAAAAAAAB9E=" });

  assert.deepEqual(renameDesignationSchema.parse(values), {
    id: analyst,
    name: "Analyst",
    version: "AAAAAAAAB9E=",
  });
});

test("renameDesignationSchema_PageOutOfDate_RefusesAMissingIdOrVersion", () => {
  assert.deepEqual(reported(renameDesignationSchema, { id: "analyst", name: "Analyst", version: "" }), [
    "id: This page is out of date. Reload the page, then send the form again.",
    "version: This page is out of date. Reload the page, then send the form again.",
  ]);
});

test("designationProblemMessages_Refusals_AreWordedForBothForms", () => {
  assert.equal(addDesignationProblemMessages["designation.name-taken"], designationNameTaken);
  assert.equal(renameDesignationProblemMessages["designation.name-taken"], designationNameTaken);
  assert.match(addDesignationProblemMessages["concurrency.conflict"] ?? "", /^Someone added a designation/);
  assert.match(
    renameDesignationProblemMessages["concurrency.conflict"] ?? "",
    /Reload the page, then try again\.$/,
  );
  assert.match(renameDesignationProblemMessages["designation.not-found"] ?? "", /no longer exists/);
});
