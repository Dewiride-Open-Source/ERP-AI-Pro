import assert from "node:assert/strict";
import { test } from "node:test";

import { listApiParameters } from "@/shared/lists/list-api";
import { readListQuery } from "@/shared/lists/list-query";

import { purchaseBillsList } from "./purchase-bills.list.ts";

test("purchaseBillsList_EveryFilterAndSort_MapsOntoItsFields", () => {
  const { query, canonical } = readListQuery(
    {
      supplier: "Shree Ganesh & Sons",
      status: ["paid", "overdue"],
      dueFrom: "2026-04-01",
      dueTo: "2027-03-31",
      sort: "amount:desc",
      size: "50",
      page: "2",
      hide: "state,billDate",
    },
    purchaseBillsList,
  );

  assert.equal(canonical, true);
  assert.deepEqual(query.hiddenColumns, ["state", "billDate"]);
  assert.deepEqual(listApiParameters(query, purchaseBillsList), {
    page: 2,
    pageSize: 50,
    sort: "amount:desc",
    filter:
      "supplier:contains:Shree%20Ganesh%20%26%20Sons;status:in:paid|overdue;" +
      "dueDate:gte:2026-04-01;dueDate:lte:2027-03-31",
  });
});

test("purchaseBillsList_NoParameters_ListsTenBillsByDueDate", () => {
  const { query } = readListQuery({}, purchaseBillsList);
  assert.deepEqual(listApiParameters(query, purchaseBillsList), {
    page: 1,
    pageSize: 10,
    sort: "dueDate:asc",
    filter: undefined,
  });
});
