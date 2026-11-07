import assert from "node:assert/strict";
import { test } from "node:test";

import {
  readSearch,
  readSearchedListView,
  searchByCodeOrName,
  searchedListHref,
  searchedListPage,
  searchMaxLength,
  shownSearchedList,
  type SearchedListAddress,
} from "./searched-list-view.ts";

const path = "/platform/reference-data";

const versioned: SearchedListAddress = { path, keep: { version: "2019-10-01" } };

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

test("readSearchedListView_NoParameters_IsTheWholeListOnItsFirstPage", () => {
  assert.deepEqual(readSearchedListView({}), { view: { search: undefined, page: 1 }, canonical: true });
});

test("readSearchedListView_SearchAndPage_KeepsEachAsTheCanonicalAddress", () => {
  assert.deepEqual(readSearchedListView({ q: "united states", page: "3" }), {
    view: { search: "united states", page: 3 },
    canonical: true,
  });
});

test("readSearchedListView_SearchWithSpaceAround_KeepsItWithoutTheSpaceAndIsNotCanonical", () => {
  assert.deepEqual(readSearchedListView({ q: "  india " }), {
    view: { search: "india", page: 1 },
    canonical: false,
  });
});

test("readSearchedListView_FirstPageNamed_IsNotCanonical", () => {
  assert.deepEqual(readSearchedListView({ page: "1" }), {
    view: { search: undefined, page: 1 },
    canonical: false,
  });
});

test("readSearchedListView_MalformedOrRepeatedValues_DropsThem", () => {
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
      readSearchedListView(parameters),
      { view: { search: undefined, page: 1 }, canonical: false },
      JSON.stringify(parameters),
    );
  }
});

test("readSearchedListView_ParameterTheListDoesNotKnow_IsNotCanonical", () => {
  assert.deepEqual(readSearchedListView({ q: "india", sort: "name" }), {
    view: { search: "india", page: 1 },
    canonical: false,
  });
});

test("readSearchedListView_ParameterTheCallerReads_IsLeftToTheCallerWhateverItHolds", () => {
  for (const version of ["2019-10-01", "not a date", ["2019-10-01", "2021-10-01"]]) {
    assert.deepEqual(
      readSearchedListView({ version, q: "rate", page: "2" }, ["version"]),
      { view: { search: "rate", page: 2 }, canonical: true },
      JSON.stringify(version),
    );
  }
});

test("readSearchedListView_CallerNamesItsOwnParameters_AnyOtherIsStillNotCanonical", () => {
  assert.deepEqual(readSearchedListView({ version: "2019-10-01", sort: "name" }, ["version"]), {
    view: { search: undefined, page: 1 },
    canonical: false,
  });
});

test("readSearchedListView_CallerNamesTheSearchOrThePage_TheListStillJudgesThem", () => {
  for (const parameters of [{ q: ["india", "india"] }, { page: ["2", "3"] }]) {
    assert.deepEqual(
      readSearchedListView(parameters, ["q", "page"]),
      { view: { search: undefined, page: 1 }, canonical: false },
      JSON.stringify(parameters),
    );
  }
});

test("readSearch_TextOfTheLongestLength_IsKept", () => {
  const longest = "x".repeat(searchMaxLength);

  assert.equal(readSearch(` ${longest} `), longest);
  assert.equal(readSearch(""), undefined);
});

test("searchedListHref_View_NamesOnlyWhatDiffersFromTheDefault", () => {
  assert.equal(searchedListHref({ path }, { search: undefined, page: 1 }), path);
  assert.equal(searchedListHref({ path }, { search: undefined, page: 2 }), `${path}?page=2`);
  assert.equal(
    searchedListHref({ path }, { search: "united states", page: 3 }),
    `${path}?q=united+states&page=3`,
  );
});

test("searchedListHref_KeptParameters_LeadEveryAddressBeforeTheSearchAndThePage", () => {
  assert.equal(searchedListHref(versioned, { search: undefined, page: 1 }), `${path}?version=2019-10-01`);
  assert.equal(
    searchedListHref(versioned, { search: undefined, page: 4 }),
    `${path}?version=2019-10-01&page=4`,
  );
  assert.equal(
    searchedListHref(versioned, { search: "rate & tax", page: 2 }),
    `${path}?version=2019-10-01&q=rate+%26+tax&page=2`,
  );
  assert.equal(
    searchedListHref({ path, keep: { version: "2019-10-01", section: "9983" } }, { search: "it", page: 1 }),
    `${path}?version=2019-10-01&section=9983&q=it`,
  );
});

test("searchedListHref_NoParameterToKeep_IsTheAddressWithoutThem", () => {
  assert.equal(searchedListHref({ path, keep: {} }, { search: "india", page: 1 }), `${path}?q=india`);
});

test("searchedListHref_CanonicalAddress_ReadsBackAsTheSameView", () => {
  const view = { search: "côte d'ivoire & co", page: 4 };
  const address = new URL(searchedListHref({ path }, view), "https://erp.example");

  assert.deepEqual(readSearchedListView(Object.fromEntries(address.searchParams)), {
    view,
    canonical: true,
  });
});

test("searchedListHref_CanonicalAddressWithKeptParameters_ReadsBackAsTheSameViewAndKeepsThem", () => {
  const view = { search: "rate", page: 2 };
  const address = new URL(searchedListHref(versioned, view), "https://erp.example");

  assert.deepEqual(readSearchedListView(Object.fromEntries(address.searchParams), ["version"]), {
    view,
    canonical: true,
  });
  assert.equal(address.searchParams.get("version"), "2019-10-01");
});

test("searchByCodeOrName_NoSearch_KeepsEveryItemInItsOrder", () => {
  assert.equal(searchByCodeOrName(countries, undefined, byCodeAndName), countries);
  assert.equal(searchByCodeOrName(countries, "   ", byCodeAndName), countries);
});

test("searchByCodeOrName_Name_MatchesWithoutCaseOrAccents", () => {
  assert.deepEqual(names(searchByCodeOrName(countries, "COTE", byCodeAndName)), ["Côte d'Ivoire"]);
  assert.deepEqual(names(searchByCodeOrName(countries, "argentina", byCodeAndName)), ["Argentina"]);
});

test("searchByCodeOrName_SeveralWords_FindsTheItemsHoldingEveryWordInTheCodeOrTheName", () => {
  assert.deepEqual(names(searchByCodeOrName(countries, "republic korea", byCodeAndName)), [
    "Korea (Democratic People's Republic of)",
    "Korea, Republic of",
  ]);
  assert.deepEqual(names(searchByCodeOrName(countries, "kr republic", byCodeAndName)), [
    "Korea, Republic of",
  ]);
});

test("searchByCodeOrName_CodeTypedInFull_ComesBeforeTheOtherMatchesInTheirOrder", () => {
  assert.deepEqual(names(searchByCodeOrName(countries, "in", byCodeAndName)), ["India", "Argentina"]);
  assert.deepEqual(names(searchByCodeOrName(countries, "Us", byCodeAndName)), ["United States of America"]);
});

test("searchByCodeOrName_NothingHoldsTheWords_FindsNothing", () => {
  assert.deepEqual(searchByCodeOrName(countries, "atlantis", byCodeAndName), []);
});

test("searchedListPage_PageInRange_ShowsItsItemsAndCounts", () => {
  assert.deepEqual(searchedListPage(countries, 2, 4), {
    items: countries.slice(4),
    page: { page: 2, pageSize: 4, totalCount: 6, pageCount: 2 },
  });
});

test("searchedListPage_PagePastTheLast_ShowsTheLastPage", () => {
  assert.deepEqual(searchedListPage(countries, 9, 4).page, {
    page: 2,
    pageSize: 4,
    totalCount: 6,
    pageCount: 2,
  });
});

test("searchedListPage_EmptyList_IsOnePageWithNothingOnIt", () => {
  assert.deepEqual(searchedListPage([], 3, 20), {
    items: [],
    page: { page: 1, pageSize: 20, totalCount: 0, pageCount: 1 },
  });
});

test("shownSearchedList_Search_PagesTheMatchesOnly", () => {
  const shown = shownSearchedList(countries, { search: "korea", page: 2 }, 1, byCodeAndName);

  assert.deepEqual(names(shown.items), ["Korea, Republic of"]);
  assert.deepEqual(shown.page, { page: 2, pageSize: 1, totalCount: 2, pageCount: 2 });
});
