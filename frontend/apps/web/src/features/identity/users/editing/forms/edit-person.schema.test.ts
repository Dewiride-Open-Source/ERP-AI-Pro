import assert from "node:assert/strict";
import { test } from "node:test";

import { editPersonProblemMessages, editPersonSchema, type EditPersonValues } from "./edit-person.schema.ts";

const edited: EditPersonValues = {
  id: "0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e",
  employeeCode: " DW-0042 ",
  phoneNumber: "+91 98765 43210",
  dateOfJoining: "2026-04-01",
  version: "AAAAAAAAB9E=",
};

function reported(values: unknown): string[] {
  const result = editPersonSchema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("editPersonSchema_EditedValues_ParsesThemWithTheRecordAndItsVersion", () => {
  assert.deepEqual(editPersonSchema.parse(edited), {
    id: "0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e",
    employeeCode: "DW-0042",
    phoneNumber: "+91 98765 43210",
    dateOfJoining: "2026-04-01",
    version: "AAAAAAAAB9E=",
  });
});

test("editPersonSchema_EveryDetailCleared_LeavesThemAbsent", () => {
  const parsed = editPersonSchema.parse({ ...edited, employeeCode: "", phoneNumber: " ", dateOfJoining: "" });

  assert.equal(parsed.employeeCode, undefined);
  assert.equal(parsed.phoneNumber, undefined);
  assert.equal(parsed.dateOfJoining, undefined);
  assert.equal(parsed.version, "AAAAAAAAB9E=");
});

test("editPersonSchema_RecordOrVersionMissing_AsksForAReload", () => {
  const reload = "This page is out of date. Reload the page, then send the form again.";

  assert.deepEqual(reported({ ...edited, id: "not-a-person" }), [`id: ${reload}`]);
  assert.deepEqual(reported({ ...edited, version: "" }), [`version: ${reload}`]);
});

test("editPersonSchema_DetailsTheApiWouldRefuse_ReportsEachOnItsField", () => {
  assert.deepEqual(
    reported({ ...edited, employeeCode: "/0042", phoneNumber: "12345", dateOfJoining: "2026-02-30" }),
    [
      "employeeCode: Use letters, digits, hyphens, slashes and underscores, starting with a letter or a digit.",
      "phoneNumber: Enter a phone number with + and the country code, such as +44 20 7946 0958, or a 10-digit Indian mobile number.",
      "dateOfJoining: Enter a real date as day-month-year, for example 31-03-2026.",
    ],
  );
});

test("editPersonProblemMessages_ConflictAndATakenCode_AreWordedForThePerson", () => {
  assert.equal(
    editPersonProblemMessages["concurrency.conflict"],
    "Someone else changed this record after the page was loaded. Reload the page, then make your changes again.",
  );
  assert.equal(
    editPersonProblemMessages["user.employee-code-taken"],
    "Another person already has this employee code.",
  );
});
