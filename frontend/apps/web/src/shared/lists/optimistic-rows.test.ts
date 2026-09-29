import assert from "node:assert/strict";
import { test } from "node:test";

import { withoutRow, type ShownRows } from "./optimistic-rows.ts";

type Row = { readonly id: string };

const idOf = (row: Row) => row.id;

function shown(ids: readonly string[], totalCount: number, pageSize = 2, page = 1): ShownRows<Row> {
  return {
    rows: ids.map((id) => ({ id })),
    page: { page, pageSize, totalCount, pageCount: Math.ceil(totalCount / pageSize) },
  };
}

test("withoutRow_ShownRow_LeavesTheOthersAndCountsOneFewer", () => {
  const result = withoutRow(shown(["a", "b"], 5), "a", idOf);

  assert.deepEqual(
    result.rows.map((row) => row.id),
    ["b"],
  );
  assert.deepEqual(result.page, { page: 1, pageSize: 2, totalCount: 4, pageCount: 2 });
});

test("withoutRow_LastRowOfTheLastPage_DropsThatPage", () => {
  const result = withoutRow(shown(["c", "d"], 4, 2, 2), "d", idOf);

  assert.deepEqual(
    result.rows.map((row) => row.id),
    ["c"],
  );
  assert.deepEqual(result.page, { page: 2, pageSize: 2, totalCount: 3, pageCount: 2 });
});

test("withoutRow_OnlyRowOfALaterPage_StaysUntilTheServerAnswers", () => {
  const before = shown(["e"], 5, 2, 3);

  assert.equal(withoutRow(before, "e", idOf), before);
});

test("withoutRow_OnlyRow_LeavesAnEmptyList", () => {
  const result = withoutRow(shown(["a"], 1), "a", idOf);

  assert.deepEqual(result.rows, []);
  assert.equal(result.page.totalCount, 0);
  assert.equal(result.page.pageCount, 0);
});

test("withoutRow_RowNotShown_ReturnsTheSameRows", () => {
  const before = shown(["a", "b"], 5);

  assert.equal(withoutRow(before, "z", idOf), before);
});
