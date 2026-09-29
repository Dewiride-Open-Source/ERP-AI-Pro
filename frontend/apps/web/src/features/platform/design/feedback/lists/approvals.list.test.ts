import assert from "node:assert/strict";
import { test } from "node:test";

import { readListQuery } from "@/shared/lists/list-query";

import { approvalsList } from "./approvals.list.ts";

test("approvalsList_NoParameters_ListsTheOldestRequestFirst", () => {
  const { query, canonical } = readListQuery({}, approvalsList);

  assert.equal(canonical, true);
  assert.deepEqual(query.sort, { field: "raisedOn", direction: "asc" });
  assert.equal(query.pageSize, 10);
});

test("approvalsList_SortByRequesterWithTheDateHidden_IsCanonical", () => {
  const { query, canonical } = readListQuery({ sort: "requestedBy:desc", hide: "raisedOn" }, approvalsList);

  assert.equal(canonical, true);
  assert.deepEqual(query.sort, { field: "requestedBy", direction: "desc" });
  assert.deepEqual(query.hiddenColumns, ["raisedOn"]);
});
