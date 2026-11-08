import assert from "node:assert/strict";
import { test } from "node:test";

import type { ParameterColumn, ParameterRow } from "../../parameter-sets/components/set-items.ts";

import { changedCells } from "./changed-cells.ts";

const columns: readonly ParameterColumn[] = [
  { name: "code", title: "Code", kind: "code", isKey: true, isRowTitle: false, choices: [] },
  { name: "wording", title: "Wording", kind: "text", isKey: false, isRowTitle: false, choices: [] },
  { name: "share", title: "Share", kind: "percent", isKey: false, isRowTitle: false, choices: [] },
  { name: "note", title: "Note", kind: "text", isKey: false, isRowTitle: false, choices: [] },
];

function row(values: {
  readonly from: string;
  readonly wording: string;
  readonly share: string;
  readonly note: string | null;
  readonly source: string;
}): ParameterRow {
  return {
    key: "alpha-1",
    from: values.from,
    until: null,
    source: values.source,
    cells: [
      { column: "code", value: "alpha-1", stateName: null },
      { column: "wording", value: values.wording, stateName: null },
      { column: "share", value: values.share, stateName: null },
      { column: "note", value: values.note, stateName: null },
    ],
  };
}

const earlier = {
  from: "2023-04-01",
  wording: "Services other than those above.",
  share: "6",
  note: null,
  source: "Order 1 of 2023, item 2",
};

const before = row(earlier);

test("changedCells_EntryAVersionChanged_ListsOnlyTheValuesThatDifferEachBesideTheOneBefore", () => {
  const after = row({ ...earlier, from: "2025-04-01", share: "2.5" });

  assert.deepEqual(changedCells(columns, { before, after }), [{ label: "Share", value: "2.5%", was: "6%" }]);
});

test("changedCells_NewClause_AddsTheSourceLast", () => {
  const after = row({
    from: "2025-04-01",
    wording: "Services other than those above and below.",
    share: "6",
    note: null,
    source: "Order 4 of 2025, item 2",
  });

  assert.deepEqual(changedCells(columns, { before, after }), [
    {
      label: "Wording",
      value: "Services other than those above and below.",
      was: "Services other than those above.",
    },
    { label: "Source", value: "Order 4 of 2025, item 2", was: "Order 1 of 2023, item 2" },
  ]);
});

test("changedCells_EmptyValueFilledOrEmptied_ReadsAsNone", () => {
  const filled = row({ ...earlier, from: "2025-04-01", note: "Only for a registered person." });

  assert.deepEqual(changedCells(columns, { before, after: filled }), [
    { label: "Note", value: "Only for a registered person.", was: "None" },
  ]);
  assert.deepEqual(changedCells(columns, { before: filled, after: before }), [
    { label: "Note", value: "None", was: "Only for a registered person." },
  ]);
});

test("changedCells_NothingDiffers_IsEmpty", () => {
  assert.deepEqual(changedCells(columns, { before, after: { ...before, from: "2025-04-01" } }), []);
});
