import assert from "node:assert/strict";
import { test } from "node:test";

import { shownSearchedList } from "@/shared/lists/searched/searched-list-view";

import type { ParameterSetSummary, VersionSummary } from "./set-items.ts";
import {
  currentText,
  nextText,
  parameterSetsHref,
  readParameterSetsView,
  setColumns,
  setSearchKeys,
  setsPerPage,
} from "./set-list.ts";

const version = (from: string): VersionSummary => ({
  from,
  until: null,
  instruments: ["Order 2 of 2025"],
  addedCount: 1,
  changedCount: 0,
  removedCount: 0,
});

const set = (key: string, title: string, more: Partial<ParameterSetSummary> = {}): ParameterSetSummary => ({
  key,
  title,
  description: `The ${title.toLowerCase()} of a test.`,
  basis: "law",
  asOf: "deductionDate",
  current: version("2026-04-01"),
  next: null,
  entryCount: 3,
  ...more,
});

test("setColumns_AreTheSetAndWhatTellsItsStateTitledByTheSet", () => {
  assert.deepEqual(
    setColumns.map((column) => column.header),
    ["Set", "Kind", "In force today", "Next change", "Entries in force today"],
  );
  assert.deepEqual(
    setColumns.filter((column) => column.title === true).map((column) => column.id),
    ["set"],
  );
});

test("setSearchKeys_Set_SearchesItsKeyAndTheWordsOfItsTitleAndDescription", () => {
  assert.deepEqual(setSearchKeys(set("sample.rent", "Rent rates")), {
    code: "sample.rent",
    name: "Rent rates The rent rates of a test.",
  });
});

test("search_WordsOfADescription_FindTheSetsThatHoldThem", () => {
  const sets = [
    set("sample.salary", "Salary"),
    set("sample.rent", "Rent", { description: "Rent of a building." }),
  ];

  const shown = shownSearchedList(sets, { search: "building", page: 1 }, setsPerPage, setSearchKeys);

  assert.deepEqual(
    shown.items.map((item) => item.key),
    ["sample.rent"],
  );
});

test("currentText_And_nextText_SayFromWhenOrThatNothingIs", () => {
  assert.equal(currentText(set("sample.a", "A")), "From 1 April 2026");
  assert.equal(currentText(set("sample.b", "B", { current: null })), "Nothing is in force yet.");
  assert.equal(nextText(set("sample.c", "C")), "None");
  assert.equal(nextText(set("sample.d", "D", { next: version("2027-04-01") })), "From 1 April 2027");
});

test("setsPerPage_ThirtySets_MakeThreePages", () => {
  const sets = Array.from({ length: 30 }, (_, index) => set(`sample.set-${index}`, `Set ${index}`));

  const shown = shownSearchedList(sets, { search: undefined, page: 3 }, setsPerPage, setSearchKeys);

  assert.deepEqual(shown.page, { page: 3, pageSize: 10, totalCount: 30, pageCount: 3 });
  assert.equal(shown.items.length, 10);
});

test("readParameterSetsView_And_parameterSetsHref_KeepTheSearchAndThePageInTheAddress", () => {
  const { view, canonical } = readParameterSetsView({ q: " tds ", page: "2" });

  assert.equal(canonical, false);
  assert.deepEqual(view, { search: "tds", page: 2 });
  assert.equal(parameterSetsHref(view), "/compliance/statutory-parameters/parameter-sets?q=tds&page=2");
  assert.equal(
    parameterSetsHref({ search: undefined, page: 1 }),
    "/compliance/statutory-parameters/parameter-sets",
  );
});
