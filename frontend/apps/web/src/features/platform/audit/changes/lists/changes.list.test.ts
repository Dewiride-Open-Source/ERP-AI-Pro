import assert from "node:assert/strict";
import { test } from "node:test";

import { listApiParameters } from "@/shared/lists/list-api";
import { readListQuery } from "@/shared/lists/list-query";

import { changeKeys, changesList } from "./changes.list.ts";

const actor = "0192f3c4-5b6a-7d8e-9f01-23456789abcd";

const record = "0192F3C4-5B6A-7D8E-9F01-23456789ABCE";

test("changesList_EveryFilter_MapsOntoTheApiListFields", () => {
  const { query, canonical } = readListQuery(
    {
      type: ["identity.users.positions", "identity.users.people"],
      action: ["deleted", "created"],
      changedFrom: "2026-10-01",
      changedTo: "2026-10-16",
      actor,
      record,
      sort: "occurredAt:asc",
      size: "50",
      page: "2",
    },
    changesList,
  );

  assert.equal(canonical, true);
  assert.deepEqual(listApiParameters(query, changesList), {
    page: 2,
    pageSize: 50,
    sort: "occurredAt:asc",
    filter:
      "entityType:in:identity.users.people|identity.users.positions;operation:in:created|deleted;" +
      "occurredAt:gte:2026-09-30T18%3A30%3A00.000Z;occurredAt:lt:2026-10-16T18%3A30%3A00.000Z;" +
      `actorId:eq:${actor};subjectId:eq:${record}`,
  });
});

test("changesList_NoParameters_ListsTheNewestChangesTwentyAPage", () => {
  const { query } = readListQuery({}, changesList);

  assert.deepEqual(listApiParameters(query, changesList), {
    page: 1,
    pageSize: 20,
    sort: "occurredAt:desc",
    filter: undefined,
  });
});

test("changesList_EachChangeKey_FiltersByTheKindOfRecord", () => {
  for (const key of changeKeys) {
    const { query, canonical } = readListQuery({ type: key }, changesList);

    assert.equal(canonical, true, key);
    assert.equal(listApiParameters(query, changesList).filter, `entityType:in:${key}`, key);
  }
});

test("changesList_AnIdThatIsNotAUuid_IsLeftOutOfTheAddress", () => {
  const notHexadecimal = `g${actor.slice(1)}`;
  for (const id of [
    "not-an-id",
    notHexadecimal,
    actor.slice(0, 35),
    actor.replaceAll("-", ""),
    `{${actor}}`,
  ]) {
    const { query, canonical } = readListQuery({ actor: id, record: id }, changesList);

    assert.equal(canonical, false, id);
    assert.deepEqual(query.filters, {}, id);
  }
});

test("changesList_AKindOfRecordOrChangeItDoesNotOffer_IsLeftOutOfTheAddress", () => {
  const { query, canonical } = readListQuery(
    { type: "identity.users.roles", action: ["renamed", "deleted"] },
    changesList,
  );

  assert.equal(canonical, false);
  assert.deepEqual(query.filters, { action: ["deleted"] });
});
