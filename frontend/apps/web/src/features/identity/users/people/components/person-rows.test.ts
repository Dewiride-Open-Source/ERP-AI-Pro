import assert from "node:assert/strict";
import { test } from "node:test";

import type { PersonResponse } from "@dewiride/erp-api-client";

import { filterOptions, personRows } from "./person-rows.ts";

const signedInAt = new Date("2026-10-17T07:55:00Z");

function person(values: Partial<PersonResponse>): PersonResponse {
  return {
    id: "0192f3c4-5b6a-7d8e-9f01-23456789abcd",
    displayName: "Asha Rao",
    workEmail: "asha@example.com",
    status: "active",
    roles: [],
    ...values,
  };
}

test("personRows_APersonWithEveryDetail_KeepsEachAsAPlainValue", () => {
  const [row] = personRows([
    person({
      employeeCode: "EMP-001",
      departmentName: "Accounts",
      designationName: "Analyst",
      managerDisplayName: "Vikram Iyer",
      dateOfJoining: { year: 2026, month: 4, day: 1, toString: () => "2026-04-01" },
      status: "deactivated",
      roles: ["finance", "manager"],
      lastSignedInAt: signedInAt,
    }),
  ]);

  assert.deepEqual(row, {
    id: "0192f3c4-5b6a-7d8e-9f01-23456789abcd",
    displayName: "Asha Rao",
    workEmail: "asha@example.com",
    employeeCode: "EMP-001",
    departmentName: "Accounts",
    designationName: "Analyst",
    managerDisplayName: "Vikram Iyer",
    dateOfJoining: "2026-04-01",
    status: "deactivated",
    roles: ["finance", "manager"],
    lastSignedInAt: signedInAt,
  });
});

test("personRows_DetailsTheApiLeftOutOrSentAsNull_AreUndefined", () => {
  const [row] = personRows([
    person({
      workEmail: null,
      employeeCode: null,
      departmentName: null,
      designationName: null,
      managerDisplayName: null,
      dateOfJoining: null,
      status: null,
      roles: null,
      lastSignedInAt: null,
    }),
  ]);

  assert.deepEqual(row, {
    id: "0192f3c4-5b6a-7d8e-9f01-23456789abcd",
    displayName: "Asha Rao",
    workEmail: undefined,
    employeeCode: undefined,
    departmentName: undefined,
    designationName: undefined,
    managerDisplayName: undefined,
    dateOfJoining: undefined,
    status: undefined,
    roles: [],
    lastSignedInAt: undefined,
  });
});

test("personRows_APersonWithoutAnIdOrAName_IsLeftOut", () => {
  const rows = personRows([
    person({ id: null }),
    person({ displayName: "" }),
    person({ id: "0192f3c4-5b6a-7d8e-9f01-23456789abce", displayName: "Vikram Iyer" }),
  ]);

  assert.deepEqual(
    rows.map((row) => row.displayName),
    ["Vikram Iyer"],
  );
});

test("filterOptions_ChoicesTheLookupLists_AreTheLookupItself", () => {
  const options = [
    { value: "d-accounts", label: "Finance › Accounts" },
    { value: "d-finance", label: "Finance" },
  ];

  assert.deepEqual(filterOptions(options, ["d-finance"], "Unknown department"), options);
});

test("filterOptions_AChoiceTheLookupDoesNotList_IsAddedUnderTheUnknownLabel", () => {
  assert.deepEqual(
    filterOptions([{ value: "d-finance", label: "Finance" }], ["d-gone", "d-finance"], "Unknown department"),
    [
      { value: "d-finance", label: "Finance" },
      { value: "d-gone", label: "Unknown department" },
    ],
  );
});

test("filterOptions_NoLookup_ListsOnlyTheChoices", () => {
  assert.deepEqual(filterOptions(undefined, [], "Unknown designation"), []);
  assert.deepEqual(filterOptions(undefined, ["g-analyst"], "Unknown designation"), [
    { value: "g-analyst", label: "Unknown designation" },
  ]);
});
