import assert from "node:assert/strict";
import { test } from "node:test";

import {
  branchIds,
  byName,
  countLabel,
  toggledBranch,
  withBranches,
  type TreeBranch,
} from "./disclosure-tree.ts";

const leaf = (id: string): TreeBranch => ({ id, children: [] });

const tree: readonly TreeBranch[] = [
  { id: "sales", children: [{ id: "inside", children: [leaf("inside-north")] }, leaf("field")] },
  leaf("finance"),
  { id: "operations", children: [leaf("logistics")] },
];

test("byName_NamesDifferingInCaseOrNumbers_SortsThemAsPeopleReadThem", () => {
  const sorted = [
    { id: "3", name: "Grade 10" },
    { id: "2", name: "grade 2" },
    { id: "1", name: "Analyst" },
  ].sort(byName);

  assert.deepEqual(
    sorted.map((entry) => entry.name),
    ["Analyst", "grade 2", "Grade 10"],
  );
});

test("byName_SameNameInAnotherCase_OrdersByIdSoEveryRenderAgrees", () => {
  const sorted = [
    { id: "b", name: "Sales" },
    { id: "a", name: "sales" },
  ].sort(byName);

  assert.deepEqual(
    sorted.map((entry) => entry.id),
    ["a", "b"],
  );
});

test("branchIds_Tree_ListsEveryNodeWithChildrenDepthFirst", () => {
  assert.deepEqual(branchIds(tree), ["sales", "inside", "operations"]);
});

test("branchIds_NoChildrenAnywhere_IsEmpty", () => {
  assert.deepEqual(branchIds([leaf("a"), leaf("b")]), []);
});

test("toggledBranch_ClosedAndOpenBranches_OpensTheFirstAndClosesTheSecond", () => {
  const opened = toggledBranch(new Set(["operations"]), "sales");
  const closed = toggledBranch(opened, "operations");

  assert.deepEqual([...opened].sort(), ["operations", "sales"]);
  assert.deepEqual([...closed], ["sales"]);
});

test("withBranches_SomeAlreadyOpen_AddsTheOthersAndKeepsTheSetWhenNothingChanges", () => {
  const expanded: ReadonlySet<string> = new Set(["sales"]);

  assert.deepEqual([...withBranches(expanded, ["sales", "inside"])].sort(), ["inside", "sales"]);
  assert.equal(withBranches(expanded, ["sales"]), expanded);
});

test("countLabel_OneAndMany_UsesTheWordsForEachWithIndianGrouping", () => {
  assert.equal(countLabel(1, "person reports to them", "people report to them"), "1 person reports to them");
  assert.equal(countLabel(3, "person reports to them", "people report to them"), "3 people report to them");
  assert.equal(
    countLabel(123456, "department under it", "departments under it"),
    "1,23,456 departments under it",
  );
});
