import assert from "node:assert/strict";
import { test } from "node:test";

import type { ListDefinition } from "./list-definition.ts";
import {
  filterSignature,
  filtersFromForm,
  listHref,
  listLink,
  listSearch,
  readListQuery,
  withFilters,
  withHiddenColumns,
  withPage,
  withPageSize,
  withSort,
  type ListQuery,
  type SearchParameters,
} from "./list-query.ts";

const bills: ListDefinition = {
  pageSizes: [10, 20, 50],
  defaultPageSize: 20,
  sortFields: ["number", "amount", "dueDate"],
  defaultSort: { field: "dueDate", direction: "asc" },
  filters: [
    { kind: "text", param: "supplier", field: "supplierName", operator: "contains", maxLength: 20 },
    { kind: "options", param: "status", field: "status", values: ["draft", "approved", "paid"] },
    { kind: "options", param: "state", field: "stateCode" },
    { kind: "dateRange", param: "due", field: "dueDate", instant: false },
  ],
  columns: ["number", "supplier", "amount", "dueDate"],
  hideableColumns: ["supplier", "amount", "dueDate"],
};

const firstPage: ListQuery = {
  page: 1,
  pageSize: 20,
  sort: { field: "dueDate", direction: "asc" },
  filters: {},
  hiddenColumns: [],
};

function read(parameters: SearchParameters) {
  return readListQuery(parameters, bills);
}

test("readListQuery_NoParameters_IsTheCanonicalFirstPage", () => {
  assert.deepEqual(read({}), { query: firstPage, canonical: true });
});

test("readListQuery_EveryPartGiven_IsReadAndCanonical", () => {
  const result = read({
    supplier: "Kaveri Traders",
    status: ["draft", "paid"],
    state: ["KA", "MH"],
    dueFrom: "2026-04-01",
    dueTo: "2026-06-30",
    sort: "amount:desc",
    size: "50",
    page: "3",
    hide: "supplier,amount",
  });
  assert.deepEqual(result.query, {
    page: 3,
    pageSize: 50,
    sort: { field: "amount", direction: "desc" },
    filters: {
      supplier: "Kaveri Traders",
      status: ["draft", "paid"],
      state: ["KA", "MH"],
      due: { from: "2026-04-01", to: "2026-06-30" },
    },
    hiddenColumns: ["supplier", "amount"],
  });
  assert.equal(result.canonical, true);
});

const notCanonical: readonly (readonly [string, SearchParameters, Partial<ListQuery>])[] = [
  ["UnknownParameter", { tab: "open" }, {}],
  ["EmptyFilter", { supplier: "  " }, {}],
  ["PaddedText", { supplier: " Kaveri " }, { filters: { supplier: "Kaveri" } }],
  ["TextLongerThanTheLimit", { supplier: "x".repeat(21) }, {}],
  ["RepeatedTextParameter", { supplier: ["a", "b"] }, {}],
  ["OptionOutsideTheAllowedValues", { status: ["paid", "cancelled"] }, { filters: { status: ["paid"] } }],
  ["RepeatedOption", { state: ["KA", "KA"] }, { filters: { state: ["KA"] } }],
  ["DateThatIsNotACalendarDate", { dueFrom: "31-04-2026" }, {}],
  ["RangeEndingBeforeItStarts", { dueFrom: "2026-05-01", dueTo: "2026-04-30" }, {}],
  ["DefaultSortWrittenOut", { sort: "dueDate:asc" }, {}],
  ["SortOnAnUnsortableField", { sort: "supplier:asc" }, {}],
  ["SortWithoutADirection", { sort: "amount" }, {}],
  ["DefaultPageSizeWrittenOut", { size: "20" }, {}],
  ["PageSizeNotOffered", { size: "25" }, {}],
  ["FirstPageWrittenOut", { page: "1" }, {}],
  ["PageThatIsNotAWholeNumber", { page: "2.5" }, {}],
  ["PageBeyondTheApiLimit", { page: "999999999999" }, {}],
  ["UnknownHiddenColumn", { hide: "notes,amount" }, { hiddenColumns: ["amount"] }],
  ["HiddenColumnsOutOfOrder", { hide: "amount,supplier" }, { hiddenColumns: ["supplier", "amount"] }],
  ["MoreOptionsThanTheApiAccepts", { state: Array.from({ length: 101 }, (_, index) => `S${index}`) }, {}],
  ["OptionLongerThanTheLimit", { state: ["x".repeat(201), "KA"] }, { filters: { state: ["KA"] } }],
  [
    "FixedColumnAmongTheHidden",
    { hide: "number,supplier,amount,dueDate" },
    { hiddenColumns: ["supplier", "amount", "dueDate"] },
  ],
];

for (const [condition, parameters, expected] of notCanonical) {
  test(`readListQuery_${condition}_IsNotCanonical`, () => {
    const result = read(parameters);
    assert.equal(result.canonical, false);
    assert.deepEqual(result.query, { ...firstPage, ...expected });
  });
}

test("readListQuery_AsManyOptionsAsTheApiAccepts_AreKept", () => {
  const states = Array.from({ length: 100 }, (_, index) => `S${String(index).padStart(3, "0")}`);
  const result = read({ state: states });
  assert.deepEqual(result.query.filters, { state: states });
  assert.equal(result.canonical, true);
});

test("readListQuery_OptionsInAnyOrder_AreTheSameCanonicalView", () => {
  const result = read({ status: ["paid", "draft"], state: ["MH", "KA"] });
  assert.deepEqual(result.query.filters, { status: ["draft", "paid"], state: ["KA", "MH"] });
  assert.equal(result.canonical, true);
});

test("readListQuery_EveryColumnHidden_ShowsThemAll", () => {
  const result = readListQuery(
    { hide: "supplier,amount" },
    { ...bills, columns: ["supplier", "amount"], hideableColumns: ["supplier", "amount"] },
  );
  assert.deepEqual(result.query.hiddenColumns, []);
  assert.equal(result.canonical, false);
});

test("readListQuery_OpenEndedRange_KeepsTheGivenEnd", () => {
  const result = read({ dueTo: "2026-03-31" });
  assert.deepEqual(result.query.filters, { due: { from: "", to: "2026-03-31" } });
  assert.equal(result.canonical, true);
});

test("listSearch_CanonicalQuery_WritesFiltersSortSizePageAndHiddenColumnsInOrder", () => {
  const query: ListQuery = {
    page: 2,
    pageSize: 50,
    sort: { field: "number", direction: "asc" },
    filters: { supplier: "A&B; Co|Ltd", status: ["approved"], due: { from: "2026-04-01", to: "" } },
    hiddenColumns: ["amount"],
  };
  const search = listSearch(query, bills);
  assert.equal(
    search,
    "?supplier=A%26B%3B+Co%7CLtd&status=approved&dueFrom=2026-04-01&sort=number%3Aasc&size=50&page=2&hide=amount",
  );
  assert.deepEqual(read(Object.fromEntries(new URLSearchParams(search))).query, query);
});

test("listSearch_FirstPage_IsEmpty", () => {
  assert.equal(listSearch(firstPage, bills), "");
});

test("withSortSizeAndFilters_OnALaterPage_ReturnToTheFirstPage", () => {
  const onPageFour = withPage(firstPage, 4);
  assert.equal(onPageFour.page, 4);
  assert.equal(withSort(onPageFour, { field: "amount", direction: "desc" }).page, 1);
  assert.equal(withPageSize(onPageFour, 50).page, 1);
  assert.equal(withFilters(onPageFour, { supplier: "Kaveri" }).page, 1);
  assert.equal(withHiddenColumns(onPageFour, ["amount"]).page, 4);
});

test("filtersFromForm_SubmittedFields_AreReadLikeTheAddress", () => {
  const form = new FormData();
  form.append("supplier", "  Kaveri ");
  form.append("status", "paid");
  form.append("status", "draft");
  form.append("dueFrom", "2026-04-01");
  form.append("dueTo", "");
  form.append("sort", "amount:desc");
  assert.deepEqual(filtersFromForm(form, bills), {
    supplier: "Kaveri",
    status: ["draft", "paid"],
    due: { from: "2026-04-01", to: "" },
  });
});

test("filterSignature_PagingSortingAndHiddenColumns_AreIgnored", () => {
  const filters = { supplier: "Kaveri" };
  assert.equal(filterSignature(filters, bills), "?supplier=Kaveri");
  assert.equal(filterSignature({}, bills), "");
});

test("listHref_FirstPageAndLaterPages_KeepTheBasePath", () => {
  assert.equal(listHref("/design/data-table", firstPage, bills), "/design/data-table");
  assert.equal(listHref("/design/data-table", withPage(firstPage, 2), bills), "/design/data-table?page=2");
});

test("listLink_LaterPage_IsRelativeToTheListPage", () => {
  assert.equal(listLink("/design/data-table", firstPage, bills), "/design/data-table");
  assert.equal(listLink("/design/data-table", withPage(firstPage, 2), bills), "?page=2");
});
