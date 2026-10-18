import assert from "node:assert/strict";
import { test } from "node:test";

import {
  type DepartmentEntry,
  departmentOptions,
  departmentPath,
  designationOptions,
  personOptions,
} from "./organisation-options.ts";

const finance: DepartmentEntry = { id: "d-finance", name: "Finance", parentId: undefined };
const accounts: DepartmentEntry = { id: "d-accounts", name: "Accounts", parentId: "d-finance" };
const payables: DepartmentEntry = { id: "d-payables", name: "Payables", parentId: "d-accounts" };
const engineering: DepartmentEntry = { id: "d-engineering", name: "Engineering", parentId: undefined };

test("departmentPath_NestedDepartment_JoinsItsAncestorsFromTheTop", () => {
  const byId = new Map([finance, accounts, payables].map((department) => [department.id, department]));

  assert.equal(departmentPath(payables, byId), "Finance › Accounts › Payables");
  assert.equal(departmentPath(finance, byId), "Finance");
});

test("departmentPath_ParentMissingFromTheList_StopsAtTheLastKnownDepartment", () => {
  const orphan: DepartmentEntry = { id: "d-orphan", name: "Orphan", parentId: "d-gone" };

  assert.equal(departmentPath(orphan, new Map([[orphan.id, orphan]])), "Orphan");
});

test("departmentPath_ParentsFormACycle_NamesEachDepartmentOnce", () => {
  const first: DepartmentEntry = { id: "d-first", name: "First", parentId: "d-second" };
  const second: DepartmentEntry = { id: "d-second", name: "Second", parentId: "d-first" };
  const byId = new Map([first, second].map((department) => [department.id, department]));

  assert.equal(departmentPath(first, byId), "Second › First");
});

test("departmentOptions_Tree_LabelsEachDepartmentWithItsPathInAlphabeticalOrder", () => {
  assert.deepEqual(departmentOptions([payables, engineering, accounts, finance]), [
    { value: "d-engineering", label: "Engineering" },
    { value: "d-finance", label: "Finance" },
    { value: "d-accounts", label: "Finance › Accounts" },
    { value: "d-payables", label: "Finance › Accounts › Payables" },
  ]);
});

test("designationOptions_Designations_SortsThemByNameIgnoringCaseAndCountingNumbers", () => {
  assert.deepEqual(
    designationOptions([
      { id: "g-10", name: "Grade 10" },
      { id: "g-2", name: "grade 2" },
      { id: "a", name: "Analyst" },
    ]),
    [
      { value: "a", label: "Analyst" },
      { value: "g-2", label: "grade 2" },
      { value: "g-10", label: "Grade 10" },
    ],
  );
});

test("personOptions_PeopleAndAnExcludedPerson_ListsTheOthersWithTheirWorkEmail", () => {
  assert.deepEqual(
    personOptions(
      [
        { id: "p-asha", displayName: "Asha Rao", workEmail: "asha@example.com" },
        { id: "p-vikram", displayName: "Vikram Iyer", workEmail: undefined },
      ],
      ["p-asha"],
    ),
    [{ value: "p-vikram", label: "Vikram Iyer", description: undefined }],
  );
});

test("personOptions_SeveralExcludedPeople_LeavesEachOfThemOut", () => {
  assert.deepEqual(
    personOptions(
      [
        { id: "p-asha", displayName: "Asha Rao", workEmail: "asha@example.com" },
        { id: "p-kiran", displayName: "Kiran Das", workEmail: "kiran@example.com" },
        { id: "p-vikram", displayName: "Vikram Iyer", workEmail: "vikram@example.com" },
      ],
      ["p-vikram", "p-asha"],
    ),
    [{ value: "p-kiran", label: "Kiran Das", description: "kiran@example.com" }],
  );
});

test("personOptions_ExcludedPeopleAmongTheFirst_LeavesThemOutBeforeKeepingTheLimit", () => {
  assert.deepEqual(
    personOptions(
      [
        { id: "p-asha", displayName: "Asha Rao", workEmail: undefined },
        { id: "p-kiran", displayName: "Kiran Das", workEmail: undefined },
        { id: "p-meera", displayName: "Meera Nair", workEmail: undefined },
        { id: "p-vikram", displayName: "Vikram Iyer", workEmail: undefined },
      ],
      ["p-asha", "p-kiran"],
      1,
    ),
    [{ value: "p-meera", label: "Meera Nair", description: undefined }],
  );
});

test("personOptions_NobodyExcluded_ListsEveryone", () => {
  assert.deepEqual(personOptions([{ id: "p-asha", displayName: "Asha Rao", workEmail: undefined }]), [
    { value: "p-asha", label: "Asha Rao", description: undefined },
  ]);
});
