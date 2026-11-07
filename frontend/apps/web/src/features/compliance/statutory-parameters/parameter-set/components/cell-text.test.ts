import assert from "node:assert/strict";
import { test } from "node:test";

import type { ParameterColumn, ParameterRow } from "../../parameter-sets/components/set-items.ts";

import {
  cellText,
  entryColumns,
  entryRow,
  entrySearchKeys,
  entryTitle,
  noValue,
  sourceColumnId,
  titleColumn,
} from "./cell-text.ts";

function column(
  name: string,
  kind: ParameterColumn["kind"],
  more: Partial<ParameterColumn> = {},
): ParameterColumn {
  return { name, title: `Title of ${name}`, kind, isKey: false, isRowTitle: false, choices: [], ...more };
}

const code = column("code", "code", { isKey: true, title: "Code" });

const label = column("label", "text", { isRowTitle: true, title: "Label" });

const share = column("share", "percent");

const limit = column("limit", "amount");

const cadence = column("cadence", "choice", {
  choices: [
    { value: "everyMonth", title: "Every month" },
    { value: "everyQuarter", title: "Every quarter" },
  ],
});

const day = column("day", "integer");

const state = column("state", "stateCode");

const reference = column("reference", "code");

const note = column("note", "text");

const columns = [code, label, share, limit, cadence, day, state, reference, note];

const row: ParameterRow = {
  key: "alpha-1",
  from: "2024-04-01",
  until: null,
  source: "Order 7 of 2024, item 3",
  cells: [
    { column: "code", value: "alpha-1" },
    { column: "label", value: "Monthly statement of sales" },
    { column: "share", value: "2.25" },
    { column: "limit", value: "123456789" },
    { column: "cadence", value: "everyQuarter" },
    { column: "day", value: "28" },
    { column: "state", value: "09" },
    { column: "reference", value: "beta-2" },
    { column: "note", value: null },
  ],
};

test("cellText_EachKind_IsWrittenAsAPersonReadsIt", () => {
  assert.equal(cellText(share, "2.25"), "2.25%");
  assert.equal(cellText(share, "100"), "100%");
  assert.equal(cellText(limit, "123456789"), "₹12,34,56,789");
  assert.equal(cellText(limit, "1234.5"), "₹1,234.50");
  assert.equal(cellText(limit, "0"), "₹0");
  assert.equal(cellText(cadence, "everyQuarter"), "Every quarter");
  assert.equal(cellText(day, "28"), "28");
  assert.equal(cellText(state, "09"), "09");
  assert.equal(cellText(code, "alpha-1"), "alpha-1");
  assert.equal(cellText(note, "As written, with (brackets)."), "As written, with (brackets).");
});

test("cellText_EmptyCell_IsNone", () => {
  for (const kind of [note, share, limit, cadence, day]) assert.equal(cellText(kind, null), noValue);
  assert.equal(noValue, "None");
});

test("cellText_ChoiceTheColumnDoesNotDeclare_IsTheValueItself", () => {
  assert.equal(cellText(cadence, "everyYear"), "everyYear");
});

test("titleColumn_SetWithARowTitleColumn_IsThatColumn", () => {
  assert.equal(titleColumn(columns), label);
});

test("titleColumn_SetWithoutOne_IsTheKeyColumn", () => {
  assert.equal(titleColumn([share, code]), code);
});

test("entryColumns_Columns_AreTheSetsInOrderTitledByOneAndEndWithTheSource", () => {
  assert.deepEqual(entryColumns([code, label, share]), [
    { id: "code", header: "Code", title: false },
    { id: "label", header: "Label", title: true },
    { id: "share", header: "Title of share", title: false },
    { id: sourceColumnId, header: "Source" },
  ]);
});

test("entryRow_Row_HoldsTheTextOfEachCellAndItsSourceUnderTheEntrysKey", () => {
  assert.deepEqual(entryRow(columns, row), {
    id: "alpha-1",
    cells: {
      code: "alpha-1",
      label: "Monthly statement of sales",
      share: "2.25%",
      limit: "₹12,34,56,789",
      cadence: "Every quarter",
      day: "28",
      state: "09",
      reference: "beta-2",
      note: "None",
      [sourceColumnId]: "Order 7 of 2024, item 3",
    },
  });
});

test("entrySearchKeys_Row_SearchesTheKeyAndTheWordsOfItsTextCodesAndChoices", () => {
  assert.deepEqual(entrySearchKeys(columns, row), {
    code: "alpha-1",
    name: "alpha-1 Monthly statement of sales Every quarter beta-2",
  });
});

test("entryTitle_SetWithARowTitleColumn_IsThatCellsText", () => {
  assert.equal(entryTitle(columns, row), "Monthly statement of sales");
});

test("entryTitle_SetWithoutOne_IsTheKeyColumnsTitleAndTheKey", () => {
  assert.equal(entryTitle([code, share], row), "Code alpha-1");
});
