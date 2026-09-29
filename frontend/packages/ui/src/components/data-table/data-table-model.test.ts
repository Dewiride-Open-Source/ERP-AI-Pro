import assert from "node:assert/strict";
import { test } from "node:test";

import {
  canToggleColumn,
  firstSortDirection,
  hiddenFromVisibility,
  idsFromSelection,
  nextSortDirection,
  pageRange,
  resultsStatus,
  selectionState,
  sortDirectionLabel,
  sortingState,
  sortStatus,
  visibilityState,
  type DataTableColumn,
  type DataTableNoun,
} from "./data-table-model.ts";

type Bill = { id: string; number: string; amount: string };

const columns: readonly DataTableColumn<Bill>[] = [
  { id: "number", header: "Bill", cell: (bill) => bill.number, role: "title", hideable: false },
  { id: "supplier", header: "Supplier", cell: () => "", sort: "text" },
  { id: "amount", header: "Amount", cell: (bill) => bill.amount, sort: "number", align: "end" },
  { id: "actions", header: "Actions", cell: () => null, role: "actions" },
];

const bills: DataTableNoun = { one: "bill", other: "bills" };

test("firstSortDirection_EachKind_TextAscendsAndNumbersAndDatesDescend", () => {
  assert.equal(firstSortDirection("text"), "asc");
  assert.equal(firstSortDirection("number"), "desc");
  assert.equal(firstSortDirection("date"), "desc");
});

test("nextSortDirection_UnsortedColumn_StartsWithItsFirstDirection", () => {
  assert.equal(nextSortDirection(false, "text"), "asc");
  assert.equal(nextSortDirection(false, "date"), "desc");
});

test("nextSortDirection_SortedColumn_FlipsWithoutReturningToUnsorted", () => {
  assert.equal(nextSortDirection("asc", "number"), "desc");
  assert.equal(nextSortDirection("desc", "number"), "asc");
});

test("sortDirectionLabel_EachKind_NamesTheOrderInPlainWords", () => {
  assert.equal(sortDirectionLabel("text", "asc"), "A to Z");
  assert.equal(sortDirectionLabel("number", "desc"), "highest first");
  assert.equal(sortDirectionLabel("date", "asc"), "earliest first");
  assert.equal(sortStatus("Due date", "date", "desc"), "Sorted by Due date, latest first.");
});

test("pageRange_MiddleAndLastPages_CountFromOneAndStopAtTheTotal", () => {
  assert.deepEqual(pageRange({ page: 2, pageSize: 20, totalCount: 132, pageCount: 7 }), {
    first: 21,
    last: 40,
  });
  assert.deepEqual(pageRange({ page: 7, pageSize: 20, totalCount: 132, pageCount: 7 }), {
    first: 121,
    last: 132,
  });
  assert.deepEqual(pageRange({ page: 1, pageSize: 20, totalCount: 0, pageCount: 0 }), { first: 0, last: 0 });
});

test("resultsStatus_EachCount_SaysWhatIsShownWithIndianGrouping", () => {
  assert.equal(
    resultsStatus({ page: 1, pageSize: 20, totalCount: 0, pageCount: 0 }, bills),
    "No bills to show.",
  );
  assert.equal(
    resultsStatus({ page: 1, pageSize: 20, totalCount: 1, pageCount: 1 }, bills),
    "Showing 1 bill.",
  );
  assert.equal(
    resultsStatus({ page: 1, pageSize: 20, totalCount: 12, pageCount: 1 }, bills),
    "Showing all 12 bills.",
  );
  assert.equal(
    resultsStatus({ page: 5001, pageSize: 20, totalCount: 123_456, pageCount: 6173 }, bills),
    "Showing 1,00,001–1,00,020 of 1,23,456 bills.",
  );
});

test("sortingState_SortOrNone_IsOneTableTermOrNone", () => {
  assert.deepEqual(sortingState({ columnId: "amount", direction: "desc" }), [{ id: "amount", desc: true }]);
  assert.deepEqual(sortingState({ columnId: "supplier", direction: "asc" }), [
    { id: "supplier", desc: false },
  ]);
  assert.deepEqual(sortingState(undefined), []);
});

test("visibilityState_HiddenIds_RoundTripInColumnOrder", () => {
  const visibility = visibilityState(["amount", "supplier"]);
  assert.deepEqual(visibility, { amount: false, supplier: false });
  assert.deepEqual(hiddenFromVisibility({ ...visibility, number: true }, ["number", "supplier", "amount"]), [
    "supplier",
    "amount",
  ]);
});

test("selectionState_SelectedIds_KeepsOnlyThoseIds", () => {
  assert.deepEqual(selectionState(["b-1", "b-9"]), { "b-1": true, "b-9": true });
  assert.deepEqual(idsFromSelection({ "b-1": true, "b-9": true }), ["b-1", "b-9"]);
});

test("canToggleColumn_FixedAndActionColumns_CannotBeToggled", () => {
  assert.equal(canToggleColumn("number", columns, []), false);
  assert.equal(canToggleColumn("actions", columns, []), false);
  assert.equal(canToggleColumn("missing", columns, []), false);
  assert.equal(canToggleColumn("amount", columns, []), true);
});

test("canToggleColumn_LastVisibleDataColumn_CannotBeHidden", () => {
  const onlyHideable: readonly DataTableColumn<Bill>[] = columns.filter((column) => column.id !== "number");
  assert.equal(canToggleColumn("amount", onlyHideable, ["supplier"]), false);
  assert.equal(canToggleColumn("supplier", onlyHideable, ["supplier"]), true);
});
