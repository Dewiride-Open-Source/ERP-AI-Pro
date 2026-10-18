import assert from "node:assert/strict";
import { test } from "node:test";

import {
  addDepartmentProblemMessages,
  addDepartmentSchema,
  addDepartmentValues,
  departmentNameMaxLength,
  departmentNameTaken,
  editDepartmentProblemMessages,
  editDepartmentSchema,
  editDepartmentValues,
} from "./department.schema.ts";

const sales = "0199a3f2-7c1e-7b4a-9d2e-000000000001";

const finance = "0199a3f2-7c1e-7b4a-9d2e-000000000002";

function reported(
  schema: typeof addDepartmentSchema | typeof editDepartmentSchema,
  values: unknown,
): string[] {
  const result = schema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("addDepartmentSchema_StartingValues_AsksOnlyForTheName", () => {
  assert.deepEqual(reported(addDepartmentSchema, addDepartmentValues(undefined)), [
    "name: Enter the department's name.",
  ]);
});

test("addDepartmentSchema_NameWithSpacesAndAParent_TrimsTheName", () => {
  assert.deepEqual(addDepartmentSchema.parse({ name: "  Inside sales ", parentId: sales }), {
    name: "Inside sales",
    parentId: sales,
  });
});

test("addDepartmentSchema_ParentLeftEmpty_IsAbsentSoTheDepartmentGoesAtTheTop", () => {
  const parsed = addDepartmentSchema.parse({ ...addDepartmentValues(undefined), name: "Sales" });

  assert.equal(parsed.name, "Sales");
  assert.equal(parsed.parentId, undefined);
});

test("addDepartmentSchema_StartedUnderADepartment_KeepsThatParent", () => {
  assert.deepEqual(addDepartmentValues(sales), { name: "", parentId: sales });
});

test("addDepartmentSchema_NameOverTheLimitOrBlank_IsRefusedOnTheName", () => {
  assert.deepEqual(reported(addDepartmentSchema, { name: "   ", parentId: "" }), [
    "name: Enter the department's name.",
  ]);
  assert.deepEqual(
    reported(addDepartmentSchema, { name: "x".repeat(departmentNameMaxLength + 1), parentId: "" }),
    [`name: Use ${departmentNameMaxLength} characters or fewer.`],
  );
  assert.deepEqual(
    reported(addDepartmentSchema, { name: "x".repeat(departmentNameMaxLength), parentId: "" }),
    [],
  );
});

test("addDepartmentSchema_ParentNoListOffers_IsRefusedOnTheParent", () => {
  assert.deepEqual(reported(addDepartmentSchema, { name: "Sales", parentId: "Finance" }), [
    "parentId: Choose a department from the list.",
  ]);
});

test("editDepartmentSchema_DepartmentAsRead_ParsesItWithItsVersion", () => {
  const values = editDepartmentValues({
    id: sales,
    name: "Sales",
    parentId: finance,
    version: "AAAAAAAAB9E=",
  });

  assert.deepEqual(editDepartmentSchema.parse(values), {
    id: sales,
    name: "Sales",
    parentId: finance,
    version: "AAAAAAAAB9E=",
  });
});

test("editDepartmentSchema_TopLevelDepartment_StartsWithAnEmptyParent", () => {
  const values = editDepartmentValues({
    id: sales,
    name: "Sales",
    parentId: undefined,
    version: "AAAAAAAAB9E=",
  });

  assert.equal(values.parentId, "");
  assert.equal(editDepartmentSchema.parse(values).parentId, undefined);
});

test("editDepartmentSchema_PageOutOfDate_RefusesAMissingIdOrVersion", () => {
  assert.deepEqual(
    reported(editDepartmentSchema, { id: "sales", name: "Sales", parentId: "", version: "" }),
    [
      "id: This page is out of date. Reload the page, then send the form again.",
      "version: This page is out of date. Reload the page, then send the form again.",
    ],
  );
});

test("departmentProblemMessages_Refusals_AreWordedForBothForms", () => {
  assert.equal(addDepartmentProblemMessages["department.name-taken"], departmentNameTaken);
  assert.equal(editDepartmentProblemMessages["department.name-taken"], departmentNameTaken);
  assert.equal(
    addDepartmentProblemMessages["concurrency.conflict"],
    "Another organisation change is being saved; try again.",
  );
  assert.match(
    editDepartmentProblemMessages["concurrency.conflict"] ?? "",
    /Reload the page, then try again\.$/,
  );
  assert.match(
    editDepartmentProblemMessages["department.would-contain-itself"] ?? "",
    /^A department cannot go/,
  );
  assert.equal(addDepartmentProblemMessages["department.would-contain-itself"], undefined);
});
