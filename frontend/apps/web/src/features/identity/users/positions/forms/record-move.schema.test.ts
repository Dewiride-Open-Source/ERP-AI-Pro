import assert from "node:assert/strict";
import { test } from "node:test";

import {
  choiceGone,
  recordMoveProblemMessages,
  recordMoveSchema,
  recordMoveValues,
  type RecordMoveValues,
} from "./record-move.schema.ts";

const personId = "0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e";

const move: RecordMoveValues = {
  personId,
  effectiveFrom: "2026-07-01",
  departmentId: "0199a3f2-7c1e-7b4a-9d2e-000000000001",
  designationId: "0199a3f2-7c1e-7b4a-9d2e-000000000002",
  managerId: "0199a3f2-7c1e-7b4a-9d2e-000000000003",
};

function reported(values: unknown): string[] {
  const result = recordMoveSchema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("recordMoveSchema_StartingValues_AsksOnlyForTheDate", () => {
  assert.deepEqual(reported(recordMoveValues(personId)), [
    "effectiveFrom: Enter the date the move takes effect.",
  ]);
});

test("recordMoveSchema_CompleteMove_ParsesItAsChosen", () => {
  assert.deepEqual(recordMoveSchema.parse(move), move);
});

test("recordMoveSchema_ChoicesLeftEmpty_AreAbsent", () => {
  const parsed = recordMoveSchema.parse({ ...move, departmentId: "", designationId: "", managerId: "" });

  assert.equal(parsed.effectiveFrom, "2026-07-01");
  assert.equal(parsed.departmentId, undefined);
  assert.equal(parsed.designationId, undefined);
  assert.equal(parsed.managerId, undefined);
});

test("recordMoveSchema_ValuesNoListOffers_AreRefusedOnTheirFields", () => {
  assert.deepEqual(
    reported({
      ...move,
      effectiveFrom: "2026-02-29",
      departmentId: "Finance",
      designationId: "Analyst",
      managerId: "Asha Rao",
    }),
    [
      "effectiveFrom: Enter a real date as day-month-year, for example 31-03-2026.",
      "departmentId: Choose a department from the list.",
      "designationId: Choose a designation from the list.",
      "managerId: Choose a manager from the list.",
    ],
  );
});

test("recordMoveProblemMessages_ChoiceDeletedMeanwhile_AsksToChooseAgain", () => {
  for (const code of [
    "position.department-not-found",
    "position.designation-not-found",
    "position.manager-not-found",
  ]) {
    assert.equal(recordMoveProblemMessages[code], choiceGone, code);
  }
  assert.equal(
    recordMoveProblemMessages["position.date-taken"],
    "A move from this date already exists; withdraw it first.",
  );
  assert.equal(
    recordMoveProblemMessages["concurrency.conflict"],
    "Another organisation change is being saved; try again.",
  );
});
