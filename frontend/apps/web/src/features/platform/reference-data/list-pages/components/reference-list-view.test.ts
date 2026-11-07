import assert from "node:assert/strict";
import { test } from "node:test";

import {
  readReferenceListView,
  readSearch,
  referenceListHref,
  referenceListPage,
  searchMaxLength,
  searchReferenceList,
  shownReferenceList,
} from "./reference-list-view.ts";

const path = "/platform/reference-data";

const countries = [
  { code: "AR", name: "Argentina" },
  { code: "CI", name: "Côte d'Ivoire" },
  { code: "IN", name: "India" },
  { code: "KP", name: "Korea (Democratic People's Republic of)" },
  { code: "KR", name: "Korea, Republic of" },
  { code: "US", name: "United States of America" },
] as const;

const byCodeAndName = (country: { readonly code: string; readonly name: string }) => country;

const names = (items: readonly { readonly name: string }[]) => items.map((item) => item.name);

test("readReferenceListView_NoParameters_IsTheWholeListOnItsFirstPage", () => {
  assert.deepEqual(readReferenceListView({}), { view: { search: undefined, page: 1 }, canonical: true });
});

test("readReferenceListView_SearchAndPage_KeepsEachAsTheCanonicalAddress", () => {
  assert.deepEqual(readReferenceListView({ q: "united states", page: "3" }), {
    view: { search: "united states", page: 3 },
    canonical: true,
  });
});

test("readReferenceListView_SearchWithSpaceAround_KeepsItWithoutTheSpaceAndIsNotCanonical", () => {
  assert.deepEqual(readReferenceListView({ q: "  india " }), {
    view: { search: "india", page: 1 },
    canonical: false,
  });
});

test("readReferenceListView_FirstPageNamed_IsNotCanonical", () => {
  assert.deepEqual(readReferenceListView({ page: "1" }), {
    view: { search: undefined, page: 1 },
    canonical: false,
  });
});

test("readReferenceListView_MalformedOrRepeatedValues_DropsThem", () => {
  for (const parameters of [
    { q: "" },
    { q: "   " },
    { q: "x".repeat(searchMaxLength + 1) },
    { q: ["india", "india"] },
    { page: "0" },
    { page: "01" },
    { page: "two" },
    { page: "1000000" },
    { page: ["2", "3"] },
  ]) {
    assert.deepEqual(
      readReferenceListView(parameters),
      { view: { search: undefined, page: 1 }, canonical: false },
      JSON.stringify(parameters),
    );
  }
});

test("readReferenceListView_ParameterTheListDoesNotKnow_IsNotCanonical", () => {
  assert.deepEqual(readReferenceListView({ q: "india", sort: "name" }), {
    view: { search: "india", page: 1 },
    canonical: false,
  });
});

test("readSearch_TextOfTheLongestLength_IsKept", () => {
  const longest = "x".repeat(searchMaxLength);

  assert.equal(readSearch(` ${longest} `), longest);
  assert.equal(readSearch(""), undefined);
});

test("referenceListHref_View_NamesOnlyWhatDiffersFromTheDefault", () => {
  assert.equal(referenceListHref(path, { search: undefined, page: 1 }), path);
  assert.equal(referenceListHref(path, { search: undefined, page: 2 }), `${path}?page=2`);
  assert.equal(
    referenceListHref(path, { search: "united states", page: 3 }),
    `${path}?q=united+states&page=3`,
  );
});

test("referenceListHref_CanonicalAddress_ReadsBackAsTheSameView", () => {
  const view = { search: "côte d'ivoire & co", page: 4 };
  const address = new URL(referenceListHref(path, view), "https://erp.example");

  assert.deepEqual(readReferenceListView(Object.fromEntries(address.searchParams)), {
    view,
    canonical: true,
  });
});

test("searchReferenceList_NoSearch_KeepsEveryItemInItsOrder", () => {
  assert.equal(searchReferenceList(countries, undefined, byCodeAndName), countries);
  assert.equal(searchReferenceList(countries, "   ", byCodeAndName), countries);
});

test("searchReferenceList_Name_MatchesWithoutCaseOrAccents", () => {
  assert.deepEqual(names(searchReferenceList(countries, "COTE", byCodeAndName)), ["Côte d'Ivoire"]);
  assert.deepEqual(names(searchReferenceList(countries, "argentina", byCodeAndName)), ["Argentina"]);
});

test("searchReferenceList_SeveralWords_FindsTheItemsHoldingEveryWordInTheCodeOrTheName", () => {
  assert.deepEqual(names(searchReferenceList(countries, "republic korea", byCodeAndName)), [
    "Korea (Democratic People's Republic of)",
    "Korea, Republic of",
  ]);
  assert.deepEqual(names(searchReferenceList(countries, "kr republic", byCodeAndName)), [
    "Korea, Republic of",
  ]);
});

test("searchReferenceList_CodeTypedInFull_ComesBeforeTheOtherMatchesInTheirOrder", () => {
  assert.deepEqual(names(searchReferenceList(countries, "in", byCodeAndName)), ["India", "Argentina"]);
  assert.deepEqual(names(searchReferenceList(countries, "Us", byCodeAndName)), ["United States of America"]);
});

test("searchReferenceList_NothingHoldsTheWords_FindsNothing", () => {
  assert.deepEqual(searchReferenceList(countries, "atlantis", byCodeAndName), []);
});

test("referenceListPage_PageInRange_ShowsItsItemsAndCounts", () => {
  assert.deepEqual(referenceListPage(countries, 2, 4), {
    items: countries.slice(4),
    page: { page: 2, pageSize: 4, totalCount: 6, pageCount: 2 },
  });
});

test("referenceListPage_PagePastTheLast_ShowsTheLastPage", () => {
  assert.deepEqual(referenceListPage(countries, 9, 4).page, {
    page: 2,
    pageSize: 4,
    totalCount: 6,
    pageCount: 2,
  });
});

test("referenceListPage_EmptyList_IsOnePageWithNothingOnIt", () => {
  assert.deepEqual(referenceListPage([], 3, 20), {
    items: [],
    page: { page: 1, pageSize: 20, totalCount: 0, pageCount: 1 },
  });
});

test("shownReferenceList_Search_PagesTheMatchesOnly", () => {
  const shown = shownReferenceList(countries, { search: "korea", page: 2 }, 1, byCodeAndName);

  assert.deepEqual(names(shown.items), ["Korea, Republic of"]);
  assert.deepEqual(shown.page, { page: 2, pageSize: 1, totalCount: 2, pageCount: 2 });
});
