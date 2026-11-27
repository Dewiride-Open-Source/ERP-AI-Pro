import assert from "node:assert/strict";
import { test } from "node:test";

import { listApiParameters } from "@/shared/lists/list-api";
import { readListQuery } from "@/shared/lists/list-query";

import {
  allRequestsList,
  approvalStates,
  decidedRequestsList,
  mineRequestsList,
  requestLists,
  waitingRequestsList,
} from "./requests.list.ts";

const answeredLists = [mineRequestsList, decidedRequestsList, allRequestsList];

const managerKind = "test.approvals.manager-03";

const chainKind = "test.approvals.chain-07";

test("waitingRequestsList_NoParameters_ListsTheLateFirstTenAPage", () => {
  const { query, canonical } = readListQuery({}, waitingRequestsList);

  assert.equal(canonical, true);
  assert.deepEqual(listApiParameters(query, waitingRequestsList), {
    page: 1,
    pageSize: 10,
    sort: "dueAt:asc",
    filter: undefined,
  });
});

test("requestLists_NoParametersOutsideTheWaitingBox_ListTheNewestFirstTenAPage", () => {
  for (const definition of answeredLists) {
    const { query, canonical } = readListQuery({}, definition);

    assert.equal(canonical, true);
    assert.deepEqual(listApiParameters(query, definition), {
      page: 1,
      pageSize: 10,
      sort: "submittedAt:desc",
      filter: undefined,
    });
  }
});

test("waitingRequestsList_EveryFilter_MapsOntoTheApiListFields", () => {
  const { query, canonical } = readListQuery(
    { kind: [managerKind, chainKind], sort: "submittedAt:desc", size: "20", page: "2" },
    waitingRequestsList,
  );

  assert.equal(canonical, true);
  assert.deepEqual(listApiParameters(query, waitingRequestsList), {
    page: 2,
    pageSize: 20,
    sort: "submittedAt:desc",
    filter: `kind:in:${chainKind}|${managerKind}`,
  });
});

test("requestLists_EveryFilterOutsideTheWaitingBox_MapsOntoTheApiListFields", () => {
  for (const definition of answeredLists) {
    const { query, canonical } = readListQuery(
      { kind: managerKind, state: ["withdrawn", "returned"], sort: "closedAt:asc", size: "50" },
      definition,
    );

    assert.equal(canonical, true);
    assert.deepEqual(listApiParameters(query, definition), {
      page: 1,
      pageSize: 50,
      sort: "closedAt:asc",
      filter: `kind:in:${managerKind};state:in:returned|withdrawn`,
    });
  }
});

test("requestLists_EachState_FiltersByItsWireName", () => {
  for (const state of approvalStates) {
    const { query, canonical } = readListQuery({ state }, allRequestsList);

    assert.equal(canonical, true, state);
    assert.equal(listApiParameters(query, allRequestsList).filter, `state:in:${state}`, state);
  }
});

test("requestLists_AStateTheApiDoesNotKnow_IsLeftOutOfTheAddress", () => {
  const { query, canonical } = readListQuery({ state: ["late", "approved"] }, mineRequestsList);

  assert.equal(canonical, false);
  assert.deepEqual(query.filters, { state: ["approved"] });
});

test("waitingRequestsList_AState_IsLeftOutOfTheAddress", () => {
  const { query, canonical } = readListQuery({ state: "approved" }, waitingRequestsList);

  assert.equal(canonical, false);
  assert.deepEqual(query.filters, {});
});

test("waitingRequestsList_ASortByClosing_FallsBackToTheLateFirst", () => {
  const { query, canonical } = readListQuery({ sort: "closedAt:desc" }, waitingRequestsList);

  assert.equal(canonical, false);
  assert.deepEqual(query.sort, { field: "dueAt", direction: "asc" });
});

test("requestLists_TheDefaultSortNamed_IsLeftOutOfTheAddress", () => {
  assert.equal(readListQuery({ sort: "dueAt:asc" }, waitingRequestsList).canonical, false);
  assert.equal(readListQuery({ sort: "submittedAt:desc" }, decidedRequestsList).canonical, false);
});

test("requestLists_HiddenColumns_KeepTheRequestAndItsStanding", () => {
  const waiting = readListQuery({ hide: "request,step,dueAt" }, waitingRequestsList);
  const mine = readListQuery({ hide: "request,state,closedAt" }, mineRequestsList);

  assert.deepEqual(waiting.query.hiddenColumns, ["step"]);
  assert.deepEqual(mine.query.hiddenColumns, ["closedAt"]);
});

test("requestLists_EveryBox_HasAListOfItsOwn", () => {
  assert.deepEqual(Object.keys(requestLists).sort(), ["all", "decided", "mine", "waiting"]);
  assert.equal(new Set(Object.values(requestLists)).size, 4);
});
