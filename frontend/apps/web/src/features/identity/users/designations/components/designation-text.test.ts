import assert from "node:assert/strict";
import { test } from "node:test";

import {
  deleteDesignationProblemMessages,
  deleteLabel,
  designationDeletion,
  designationRecords,
  historyLabel,
  renameLabel,
  renamedMessage,
} from "./designation-text.ts";

test("designationRecords_ApiDesignations_KeepsNamedOnesInAlphabeticalOrder", () => {
  assert.deepEqual(
    designationRecords([
      { id: "g-10", name: "Grade 10", version: "AAAAAAAAB9E=" },
      { id: "g-2", name: "grade 2", version: null },
      { id: "a", name: "Analyst", version: "AAAAAAAAB9F=" },
      { id: null, name: "Without an id" },
      { id: "empty", name: "" },
    ]),
    [
      { id: "a", name: "Analyst", version: "AAAAAAAAB9F=" },
      { id: "g-2", name: "grade 2", version: undefined },
      { id: "g-10", name: "Grade 10", version: "AAAAAAAAB9E=" },
    ],
  );
});

test("designationLabels_ADesignation_NameItInEveryControlOfItsRow", () => {
  assert.equal(renameLabel("Analyst"), "Rename Analyst");
  assert.equal(deleteLabel("Analyst"), "Delete Analyst");
  assert.equal(historyLabel("Analyst"), "Change history of Analyst");
});

test("renamedMessage_NewOrSameName_SaysWhatChanged", () => {
  assert.equal(renamedMessage("Analyst", "Senior analyst"), "Renamed Analyst to Senior analyst.");
  assert.equal(renamedMessage("Analyst", "Analyst"), "Saved Analyst.");
});

test("designationDeletion_ADesignation_AsksAndReportsInItsName", () => {
  assert.deepEqual(designationDeletion("Analyst"), {
    title: "Delete Analyst?",
    description:
      "Analyst is removed from the designations. The moves that named it keep its name, so people's history still shows it.",
    confirm: "Delete",
    cancel: "Keep it",
    done: "Deleted Analyst.",
    gone: "Analyst is no longer recorded.",
  });
});

test("deleteDesignationProblemMessages_DesignationInUse_SaysWhatToChangeFirst", () => {
  assert.equal(
    deleteDesignationProblemMessages["designation.in-use"],
    "Someone holds this designation today or from a later date. Record another designation for them first.",
  );
  assert.equal(
    deleteDesignationProblemMessages["concurrency.conflict"],
    "Another organisation change is being saved; try again.",
  );
});
