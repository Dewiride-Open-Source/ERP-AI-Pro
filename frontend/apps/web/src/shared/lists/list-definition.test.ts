import assert from "node:assert/strict";
import { test } from "node:test";

import { defineList, filterParameters, type ListDefinition } from "./list-definition.ts";

const bills: ListDefinition = {
  pageSizes: [10, 20, 50],
  defaultPageSize: 20,
  sortFields: ["number", "amount", "dueDate"],
  defaultSort: { field: "dueDate", direction: "asc" },
  filters: [
    { kind: "text", param: "supplier", field: "supplierName", operator: "contains", maxLength: 100 },
    { kind: "options", param: "status", field: "status", values: ["draft", "paid"] },
    { kind: "dateRange", param: "due", field: "dueDate", instant: false },
  ],
  columns: ["number", "supplier", "amount", "dueDate"],
  hideableColumns: ["supplier", "amount"],
};

test("defineList_ValidDefinition_IsReturnedUnchanged", () => {
  assert.equal(defineList(bills), bills);
});

test("filterParameters_DateRange_UsesFromAndToParameters", () => {
  assert.deepEqual(filterParameters({ kind: "dateRange", param: "due", field: "dueDate", instant: false }), [
    "dueFrom",
    "dueTo",
  ]);
  assert.deepEqual(filterParameters({ kind: "options", param: "status", field: "status" }), ["status"]);
});

const invalid: readonly (readonly [string, Partial<ListDefinition>, RegExp])[] = [
  ["NoPageSizes", { pageSizes: [] }, /pageSizes is empty/],
  ["PageSizesOutOfOrder", { pageSizes: [50, 20] }, /ascending order/],
  ["PageSizeAboveTheApiLimit", { pageSizes: [20, 500] }, /from 1 to 200/],
  ["DefaultPageSizeNotOffered", { defaultPageSize: 25 }, /defaultPageSize/],
  ["DefaultSortOnAnUnsortableField", { defaultSort: { field: "supplier", direction: "asc" } }, /defaultSort/],
  [
    "SortFieldThatIsNotAnApiName",
    { sortFields: ["due-date"], defaultSort: { field: "due-date", direction: "asc" } },
    /sortFields/,
  ],
  [
    "FilterParameterNamedLikeTheSortParameter",
    {
      filters: [{ kind: "text", param: "sort", field: "supplierName", operator: "contains", maxLength: 10 }],
    },
    /distinct camelCase names/,
  ],
  [
    "DateRangeCollidingWithAnotherFilter",
    {
      filters: [
        { kind: "dateRange", param: "due", field: "dueDate", instant: false },
        { kind: "text", param: "dueFrom", field: "supplierName", operator: "eq", maxLength: 10 },
      ],
    },
    /distinct camelCase names/,
  ],
  [
    "MoreFilterTermsThanTheApiAllows",
    {
      filters: Array.from({ length: 6 }, (_, index) => ({
        kind: "dateRange" as const,
        param: `range${index}`,
        field: "dueDate",
        instant: false,
      })),
    },
    /more than 10 terms/,
  ],
  ["HideableColumnThatDoesNotExist", { hideableColumns: ["notes"] }, /hideableColumns/],
];

for (const [condition, change, message] of invalid) {
  test(`defineList_${condition}_Throws`, () => {
    assert.throws(() => defineList({ ...bills, ...change }), message);
  });
}
