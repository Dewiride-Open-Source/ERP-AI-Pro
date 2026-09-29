import assert from "node:assert/strict";
import { test } from "node:test";

import { listApiParameters } from "./list-api.ts";
import type { ListDefinition } from "./list-definition.ts";
import type { ListQuery } from "./list-query.ts";

const files: ListDefinition = {
  pageSizes: [10, 20],
  defaultPageSize: 20,
  sortFields: ["fileName", "createdAt"],
  defaultSort: { field: "createdAt", direction: "desc" },
  filters: [
    { kind: "text", param: "name", field: "fileName", operator: "contains", maxLength: 100 },
    { kind: "options", param: "type", field: "contentType" },
    { kind: "dateRange", param: "uploaded", field: "createdAt", instant: true },
    { kind: "dateRange", param: "due", field: "dueDate", instant: false },
  ],
  columns: ["fileName", "createdAt"],
  hideableColumns: ["createdAt"],
};

const firstPage: ListQuery = {
  page: 1,
  pageSize: 20,
  sort: { field: "createdAt", direction: "desc" },
  filters: {},
  hiddenColumns: ["createdAt"],
};

test("listApiParameters_NoFilters_SendsPageSizeAndTheExplicitSort", () => {
  assert.deepEqual(listApiParameters(firstPage, files), {
    page: 1,
    pageSize: 20,
    sort: "createdAt:desc",
    filter: undefined,
  });
});

test("listApiParameters_TextAndOptions_PercentEncodeEveryValue", () => {
  const { filter } = listApiParameters(
    { ...firstPage, filters: { name: "Q1; 50% off:final|v2", type: ["application/pdf", "text/csv"] } },
    files,
  );
  assert.equal(
    filter,
    "fileName:contains:Q1%3B%2050%25%20off%3Afinal%7Cv2;contentType:in:application%2Fpdf|text%2Fcsv",
  );
});

test("listApiParameters_InstantRange_CoversWholeIndiaStandardTimeDays", () => {
  const { filter } = listApiParameters(
    { ...firstPage, filters: { uploaded: { from: "2026-09-01", to: "2026-09-30" } } },
    files,
  );
  assert.equal(
    filter,
    "createdAt:gte:2026-08-31T18%3A30%3A00.000Z;createdAt:lt:2026-09-30T18%3A30%3A00.000Z",
  );
});

test("listApiParameters_InstantRangeEndingOnTheLastCalendarDay_HasNoUpperBound", () => {
  const { filter } = listApiParameters(
    { ...firstPage, filters: { uploaded: { from: "", to: "9999-12-31" } } },
    files,
  );
  assert.equal(filter, undefined);
});

test("listApiParameters_InstantRangeStartingOnTheFirstCalendarDay_HasNoLowerBound", () => {
  const { filter } = listApiParameters(
    { ...firstPage, filters: { uploaded: { from: "0001-01-01", to: "" } } },
    files,
  );
  assert.equal(filter, undefined);
});

test("listApiParameters_InstantRangeAcrossAMonthEnd_EndsAtTheNextMidnight", () => {
  const { filter } = listApiParameters(
    { ...firstPage, filters: { uploaded: { from: "", to: "2028-02-29" } } },
    files,
  );
  assert.equal(filter, "createdAt:lt:2028-02-29T18%3A30%3A00.000Z");
});

test("listApiParameters_CalendarDateRange_IncludesBothEnds", () => {
  const { filter } = listApiParameters(
    { ...firstPage, filters: { due: { from: "2026-04-01", to: "2027-03-31" } } },
    files,
  );
  assert.equal(filter, "dueDate:gte:2026-04-01;dueDate:lte:2027-03-31");
});
