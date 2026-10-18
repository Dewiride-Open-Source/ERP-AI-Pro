import assert from "node:assert/strict";
import { test } from "node:test";

import {
  addUnderLabel,
  childCountLabel,
  childListLabel,
  deleteDepartmentProblemMessages,
  deleteLabel,
  departmentDeletion,
  editLabel,
  historyLabel,
  showChildrenLabel,
} from "./department-text.ts";

test("childCountLabel_OneOrMoreDepartments_CountsThemInWords", () => {
  assert.equal(childCountLabel(1), "1 department under it");
  assert.equal(childCountLabel(4), "4 departments under it");
});

test("departmentLabels_ADepartment_NameItInEveryControlOfItsRow", () => {
  assert.equal(showChildrenLabel("Sales"), "Show the departments under Sales");
  assert.equal(childListLabel("Sales"), "Departments under Sales");
  assert.equal(addUnderLabel("Sales"), "Add a department under Sales");
  assert.equal(editLabel("Sales"), "Rename or move Sales");
  assert.equal(deleteLabel("Sales"), "Delete Sales");
  assert.equal(historyLabel("Sales"), "Change history of Sales");
});

test("departmentDeletion_ADepartment_AsksAndReportsInItsName", () => {
  assert.deepEqual(departmentDeletion("Sales"), {
    title: "Delete Sales?",
    description:
      "Sales is removed from the departments. The moves that named it keep its name, so people's history still shows it.",
    confirm: "Delete",
    cancel: "Keep it",
    done: "Deleted Sales.",
    gone: "Sales is no longer recorded.",
  });
});

test("deleteDepartmentProblemMessages_DepartmentInUse_SaysWhatToMoveFirst", () => {
  assert.equal(
    deleteDepartmentProblemMessages["department.in-use"],
    "This department still has departments under it, or someone is in it today or from a later date. Move them elsewhere first.",
  );
  assert.equal(
    deleteDepartmentProblemMessages["concurrency.conflict"],
    "Another organisation change is being saved; try again.",
  );
});
