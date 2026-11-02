import assert from "node:assert/strict";
import { test } from "node:test";

import { listApiParameters } from "@/shared/lists/list-api";
import { readListQuery } from "@/shared/lists/list-query";

import { runKeyMaxLength, runStatuses, runsList } from "./runs.list.ts";

const accountCheck = "identity.users.directory-account-check";

const occurrenceKey = "occurrence:2026-11-02T00:30+05:30";

test("runsList_EveryFilter_MapsOntoTheApiListFields", () => {
  const { query, canonical } = readListQuery(
    {
      job: [accountCheck, "test.jobs.sample"],
      status: ["deadLettered", "failed"],
      key: occurrenceKey,
      createdFrom: "2026-11-01",
      createdTo: "2026-11-02",
      sort: "dueAt:asc",
      size: "20",
      page: "2",
    },
    runsList,
  );

  assert.equal(canonical, true);
  assert.deepEqual(listApiParameters(query, runsList), {
    page: 2,
    pageSize: 20,
    sort: "dueAt:asc",
    filter:
      `jobName:in:${accountCheck}|test.jobs.sample;status:in:failed|deadLettered;` +
      "idempotencyKey:contains:occurrence%3A2026-11-02T00%3A30%2B05%3A30;" +
      "createdAt:gte:2026-10-31T18%3A30%3A00.000Z;createdAt:lt:2026-11-02T18%3A30%3A00.000Z",
  });
});

test("runsList_NoParameters_ListsTheNewestRunsTenAPage", () => {
  const { query, canonical } = readListQuery({}, runsList);

  assert.equal(canonical, true);
  assert.deepEqual(listApiParameters(query, runsList), {
    page: 1,
    pageSize: 10,
    sort: "createdAt:desc",
    filter: undefined,
  });
});

test("runsList_EachStatus_FiltersByItsWireName", () => {
  for (const status of runStatuses) {
    const { query, canonical } = readListQuery({ status }, runsList);

    assert.equal(canonical, true, status);
    assert.equal(listApiParameters(query, runsList).filter, `status:in:${status}`, status);
  }
});

test("runsList_AStatusTheApiDoesNotKnow_IsLeftOutOfTheAddress", () => {
  const { query, canonical } = readListQuery({ status: ["stuck", "pending"] }, runsList);

  assert.equal(canonical, false);
  assert.deepEqual(query.filters, { status: ["pending"] });
});

test("runsList_AKeyNoRunCouldHave_IsLeftOutOfTheAddress", () => {
  for (const key of ["two words", "someone@example.com", "key;more", "x".repeat(runKeyMaxLength + 1)]) {
    const { query, canonical } = readListQuery({ key }, runsList);

    assert.equal(canonical, false, key);
    assert.deepEqual(query.filters, {}, key);
  }
});

test("runsList_TheLongestKey_IsKept", () => {
  const key = `${"a".repeat(runKeyMaxLength - 1)}:`;
  const { query, canonical } = readListQuery({ key }, runsList);

  assert.equal(canonical, true);
  assert.equal(listApiParameters(query, runsList).filter, `idempotencyKey:contains:${"a".repeat(199)}%3A`);
});

test("runsList_ASortTheApiDoesNotOffer_FallsBackToTheNewestFirst", () => {
  const { query, canonical } = readListQuery({ sort: "jobName:asc" }, runsList);

  assert.equal(canonical, false);
  assert.deepEqual(query.sort, { field: "createdAt", direction: "desc" });
});
