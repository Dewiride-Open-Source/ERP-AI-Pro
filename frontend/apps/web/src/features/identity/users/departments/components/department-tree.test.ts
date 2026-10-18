import assert from "node:assert/strict";
import { test } from "node:test";

import {
  ancestorsOf,
  departmentRecords,
  departmentTree,
  departmentsWithin,
  parentChoices,
  type DepartmentNode,
  type DepartmentRecord,
} from "./department-tree.ts";

function department(id: string, name: string, parentId?: string): DepartmentRecord {
  return { id, name, parentId, version: `v-${id}` };
}

const sales = department("d-sales", "Sales");
const inside = department("d-inside", "Inside sales", "d-sales");
const field = department("d-field", "Field sales", "d-sales");
const north = department("d-north", "North", "d-inside");
const finance = department("d-finance", "Finance");

const records = [north, finance, inside, sales, field];

function outline(nodes: readonly DepartmentNode[], depth = 0): string[] {
  return nodes.flatMap((node) => [`${"  ".repeat(depth)}${node.name}`, ...outline(node.children, depth + 1)]);
}

test("departmentRecords_ApiDepartments_KeepsThoseWithAnIdAndANameAndNamesAbsentFieldsUndefined", () => {
  assert.deepEqual(
    departmentRecords([
      { id: "d-sales", name: "Sales", parentId: null, version: "AAAAAAAAB9E=" },
      { id: "d-inside", name: "Inside sales", parentId: "d-sales", version: null },
      { id: null, name: "Nameless id" },
      { id: "d-empty", name: "" },
    ]),
    [
      { id: "d-sales", name: "Sales", parentId: undefined, version: "AAAAAAAAB9E=" },
      { id: "d-inside", name: "Inside sales", parentId: "d-sales", version: undefined },
    ],
  );
});

test("departmentTree_Departments_NestsEachUnderItsParentAlphabeticallyAtEveryLevel", () => {
  assert.deepEqual(outline(departmentTree(records)), [
    "Finance",
    "Sales",
    "  Field sales",
    "  Inside sales",
    "    North",
  ]);
});

test("departmentTree_Departments_KeepsEachRecordWithItsOwnParentAndVersion", () => {
  const [, salesNode] = departmentTree(records);

  assert.equal(salesNode?.version, "v-d-sales");
  assert.equal(salesNode?.children[1]?.parentId, "d-sales");
  assert.equal(salesNode?.children[1]?.children[0]?.id, "d-north");
});

test("departmentTree_ParentTheListDoesNotHold_PutsTheDepartmentAtTheTop", () => {
  const orphan = department("d-orphan", "Orphan", "d-gone");

  assert.deepEqual(outline(departmentTree([sales, orphan])), ["Orphan", "Sales"]);
});

test("departmentTree_ParentsFormALoop_ShowsEachDepartmentOnceStartingFromTheFirstName", () => {
  const first = department("d-first", "First", "d-second");
  const second = department("d-second", "Second", "d-first");
  const self = department("d-self", "Self", "d-self");

  assert.deepEqual(outline(departmentTree([second, first, self])), ["First", "  Second", "Self"]);
});

test("departmentTree_NoDepartments_IsEmpty", () => {
  assert.deepEqual(departmentTree([]), []);
});

test("departmentsWithin_Department_HoldsItAndEveryDepartmentUnderItAtAnyDepth", () => {
  assert.deepEqual([...departmentsWithin(records, "d-sales")].sort(), [
    "d-field",
    "d-inside",
    "d-north",
    "d-sales",
  ]);
  assert.deepEqual([...departmentsWithin(records, "d-finance")], ["d-finance"]);
});

test("parentChoices_DepartmentBeingMoved_LeavesOutItselfAndTheDepartmentsWithinIt", () => {
  assert.deepEqual(
    parentChoices(records, "d-inside").map((option) => option.label),
    ["Finance", "Sales", "Sales › Field sales"],
  );
});

test("parentChoices_NewDepartment_OffersEveryDepartmentByItsPath", () => {
  assert.deepEqual(
    parentChoices(records, undefined).map((option) => option.label),
    ["Finance", "Sales", "Sales › Field sales", "Sales › Inside sales", "Sales › Inside sales › North"],
  );
});

test("ancestorsOf_NestedDepartment_ListsItsParentsUpToTheTop", () => {
  assert.deepEqual(ancestorsOf(records, "d-north"), ["d-inside", "d-sales"]);
  assert.deepEqual(ancestorsOf(records, "d-sales"), []);
  assert.deepEqual(ancestorsOf(records, "d-unknown"), []);
});

test("ancestorsOf_ParentsFormALoop_StopsBeforeRepeatingOne", () => {
  const first = department("d-first", "First", "d-second");
  const second = department("d-second", "Second", "d-first");

  assert.deepEqual(ancestorsOf([first, second], "d-first"), ["d-second"]);
});
