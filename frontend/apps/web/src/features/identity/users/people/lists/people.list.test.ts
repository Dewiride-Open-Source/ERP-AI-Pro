import assert from "node:assert/strict";
import { test } from "node:test";

import { listApiParameters } from "@/shared/lists/list-api";
import { listHref, readListQuery } from "@/shared/lists/list-query";

import { peopleList, withWellFormedIds } from "./people.list.ts";

const finance = "0192f3c4-5b6a-7d8e-9f01-23456789abcd";

const accounts = "0192f3c4-5b6a-7d8e-9f01-23456789abce";

const analyst = "0192f3c4-5b6a-7d8e-9f01-23456789abcf";

test("peopleList_EveryFilterAndSort_MapsOntoTheApiListFields", () => {
  const { query, canonical } = readListQuery(
    {
      name: "Asha",
      email: "asha@",
      code: "EMP-0",
      status: ["deactivated", "active"],
      department: [accounts, finance],
      designation: analyst,
      joinedFrom: "2026-04-01",
      joinedTo: "2027-03-31",
      sort: "dateOfJoining:desc",
      size: "50",
      page: "2",
      hide: "workEmail,lastSignedInAt",
    },
    peopleList,
  );

  assert.equal(canonical, true);
  assert.deepEqual(query.hiddenColumns, ["workEmail", "lastSignedInAt"]);
  assert.deepEqual(listApiParameters(query, peopleList), {
    page: 2,
    pageSize: 50,
    sort: "dateOfJoining:desc",
    filter:
      "displayName:contains:Asha;workEmail:contains:asha%40;employeeCode:contains:EMP-0;status:in:active|deactivated;" +
      `departmentId:in:${finance}|${accounts};designationId:in:${analyst};` +
      "dateOfJoining:gte:2026-04-01;dateOfJoining:lte:2027-03-31",
  });
});

test("peopleList_NoParameters_ListsTenPeopleByNameFromAToZ", () => {
  const { query } = readListQuery({}, peopleList);

  assert.deepEqual(listApiParameters(query, peopleList), {
    page: 1,
    pageSize: 10,
    sort: "displayName:asc",
    filter: undefined,
  });
});

test("peopleList_EachSortField_ReachesTheApiAsWritten", () => {
  for (const field of peopleList.sortFields) {
    const { query, canonical } = readListQuery({ sort: `${field}:desc` }, peopleList);

    assert.equal(canonical, true, field);
    assert.equal(listApiParameters(query, peopleList).sort, `${field}:desc`, field);
  }
});

test("peopleList_AStatusItDoesNotOffer_IsLeftOutOfTheAddress", () => {
  const { query, canonical } = readListQuery({ status: ["invited", "active"] }, peopleList);

  assert.equal(canonical, false);
  assert.deepEqual(query.filters, { status: ["active"] });
});

test("peopleList_TheNameColumn_CannotBeHidden", () => {
  const { query, canonical } = readListQuery({ hide: "displayName,status" }, peopleList);

  assert.equal(canonical, false);
  assert.deepEqual(query.hiddenColumns, ["status"]);
});

test("withWellFormedIds_OnlyIds_KeepsTheQueryAsItIs", () => {
  const { query } = readListQuery({ department: [finance, accounts], designation: analyst }, peopleList);

  assert.equal(withWellFormedIds(query), query);
});

test("withWellFormedIds_ValuesThatAreNotIds_AreLeftOutAndTheListStartsAgain", () => {
  const { query } = readListQuery(
    { name: "Asha", department: ["finance", finance], designation: "{analyst}", page: "3" },
    peopleList,
  );

  const kept = withWellFormedIds(query);

  assert.deepEqual(kept.filters, { name: "Asha", department: [finance] });
  assert.equal(kept.page, 1);
  assert.equal(
    listHref("/identity/users", kept, peopleList),
    `/identity/users?name=Asha&department=${finance}`,
  );
});

test("withWellFormedIds_AnIdInCapitals_IsWrittenInLowerCaseInTheCanonicalOrder", () => {
  const { query } = readListQuery({ department: [finance.toUpperCase(), accounts] }, peopleList);

  const kept = withWellFormedIds(query);

  assert.deepEqual(kept.filters, { department: [finance, accounts] });
  assert.equal(readListQuery({ department: [finance, accounts] }, peopleList).canonical, true);
});

test("withWellFormedIds_TheSameIdTwice_KeepsItOnce", () => {
  const { query } = readListQuery({ designation: [analyst, analyst.toUpperCase()] }, peopleList);

  assert.deepEqual(withWellFormedIds(query).filters, { designation: [analyst] });
});

test("withWellFormedIds_TextFiltersThatLookLikeNoId_AreKept", () => {
  const { query } = readListQuery({ name: "not-an-id", code: "EMP/7" }, peopleList);

  assert.equal(withWellFormedIds(query), query);
});
