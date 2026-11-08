import assert from "node:assert/strict";
import { test } from "node:test";

import type {
  ParameterRowResponse,
  ParameterSetResponse,
  ParameterSetSummaryResponse,
} from "@dewiride/erp-api-client";

import { apiDateOf } from "@/shared/api/dates/date-only";

import { setDetailOf, setSummaryOf } from "./set-items.ts";

const summary: ParameterSetSummaryResponse = {
  key: "sample.rates",
  title: "Sample rates",
  description: "Rates for a test.",
  basis: "law",
  asOf: "supplyDate",
  firstVersionFrom: apiDateOf("2023-04-01"),
  versionCount: 2,
  current: {
    from: apiDateOf("2025-04-01"),
    until: null,
    instruments: ["Order 2 of 2025"],
    addedCount: 1,
    changedCount: 2,
    removedCount: 0,
  },
  next: null,
  entryCount: 3,
};

function row(key: string, from: string, until: string | null, value: string | null): ParameterRowResponse {
  return {
    key,
    from: apiDateOf(from),
    until: until === null ? null : apiDateOf(until),
    source: `Order 1 of 2023, item ${key}`,
    cells: [
      { column: "code", value: key },
      { column: "note", value },
    ],
  };
}

const detail: ParameterSetResponse = {
  key: "sample.rates",
  title: "Sample rates",
  description: "Rates for a test.",
  basis: "interpretation",
  asOf: "taxPeriodStart",
  columns: [
    {
      name: "code",
      title: "Code",
      kind: "code",
      isKey: true,
      isRowTitle: false,
      isOptional: false,
      choices: [],
    },
    {
      name: "note",
      title: "Note",
      kind: "choice",
      isKey: false,
      isRowTitle: false,
      isOptional: true,
      choices: [{ value: "first", title: "The first" }],
    },
  ],
  versions: [
    {
      from: apiDateOf("2025-04-01"),
      until: null,
      instruments: ["Order 2 of 2025"],
      addedCount: 0,
      changedCount: 1,
      removedCount: 0,
    },
  ],
  currentFrom: apiDateOf("2025-04-01"),
  shown: {
    from: apiDateOf("2025-04-01"),
    until: null,
    instruments: ["Order 2 of 2025"],
    rows: [row("a", "2025-04-01", null, "first")],
    added: [],
    changed: [
      { before: row("a", "2023-04-01", "2025-03-31", null), after: row("a", "2025-04-01", null, "first") },
    ],
    removed: [],
  },
};

test("setSummaryOf_AnsweredSet_IsPlainWithItsDaysAsIsoDates", () => {
  assert.deepEqual(setSummaryOf(summary), {
    key: "sample.rates",
    title: "Sample rates",
    description: "Rates for a test.",
    basis: "law",
    asOf: "supplyDate",
    current: {
      from: "2025-04-01",
      until: null,
      instruments: ["Order 2 of 2025"],
      addedCount: 1,
      changedCount: 2,
      removedCount: 0,
    },
    next: null,
    entryCount: 3,
  });
});

test("setSummaryOf_SetWithoutAMemberTheContractRequires_Throws", () => {
  assert.throws(() => setSummaryOf({ ...summary, title: "" }), TypeError);
  assert.throws(() => setSummaryOf({ ...summary, entryCount: null }), TypeError);
  assert.throws(() => setSummaryOf({ ...summary, current: { ...summary.current, from: null } }), TypeError);
});

test("setDetailOf_AnsweredSet_KeepsItsColumnsVersionsAndTheShownVersionWithItsChanges", () => {
  const set = setDetailOf(detail);

  assert.equal(set.currentFrom, "2025-04-01");
  assert.deepEqual(set.columns[1], {
    name: "note",
    title: "Note",
    kind: "choice",
    isKey: false,
    isRowTitle: false,
    choices: [{ value: "first", title: "The first" }],
  });
  assert.deepEqual(set.versions, [
    {
      from: "2025-04-01",
      until: null,
      instruments: ["Order 2 of 2025"],
      addedCount: 0,
      changedCount: 1,
      removedCount: 0,
    },
  ]);
  assert.deepEqual(set.shown?.changed, [
    {
      before: {
        key: "a",
        from: "2023-04-01",
        until: "2025-03-31",
        source: "Order 1 of 2023, item a",
        cells: [
          { column: "code", value: "a", stateName: null },
          { column: "note", value: null, stateName: null },
        ],
      },
      after: {
        key: "a",
        from: "2025-04-01",
        until: null,
        source: "Order 1 of 2023, item a",
        cells: [
          { column: "code", value: "a", stateName: null },
          { column: "note", value: "first", stateName: null },
        ],
      },
    },
  ]);
});

test("setDetailOf_StateCodeCell_KeepsTheNameOfItsState", () => {
  const answered = row("a", "2025-04-01", null, "first");
  const named = { ...answered, cells: [{ column: "code", value: "27", stateName: "Maharashtra" }] };

  const set = setDetailOf({ ...detail, shown: { ...detail.shown, rows: [named], changed: [] } });

  assert.deepEqual(set.shown?.rows[0]?.cells, [{ column: "code", value: "27", stateName: "Maharashtra" }]);
});

test("setDetailOf_DayBeforeTheFirstVersion_ShowsNoVersion", () => {
  const set = setDetailOf({ ...detail, currentFrom: null, shown: null });

  assert.equal(set.currentFrom, null);
  assert.equal(set.shown, null);
});

test("setSummaryOf_VersionKiotaReadFromANull_IsNoVersion", () => {
  assert.equal(setSummaryOf({ ...summary, next: {} }).next, null);
  assert.equal(setSummaryOf({ ...summary, current: {} }).current, null);
});

test("setDetailOf_ShownVersionKiotaReadFromANull_ShowsNoVersion", () => {
  assert.equal(setDetailOf({ ...detail, shown: {} }).shown, null);
});
