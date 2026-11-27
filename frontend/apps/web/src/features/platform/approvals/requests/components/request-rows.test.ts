import assert from "node:assert/strict";
import { test } from "node:test";

import type { ApprovalRequestSummaryResponse } from "@dewiride/erp-api-client";

import { kindFilterNote, nonEmpty, requestRow, requestRows, waitingStepOf } from "./request-rows.ts";
import { kindsUnavailableNote } from "./request-text.ts";

const viewer = "6f2c1d7e-9a41-4b8e-9c55-0d2f7a1b3c4e";

const asha = "1b7e4f20-3c5d-4e6f-8a9b-0c1d2e3f4a5b";

const ravi = "9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a";

const submittedAt = new Date("2026-11-23T04:30:00Z");

const dueAt = new Date("2026-11-25T18:30:00Z");

const closedAt = new Date("2026-11-24T06:15:00Z");

function waitingRequest(facts: Partial<ApprovalRequestSummaryResponse> = {}): ApprovalRequestSummaryResponse {
  return {
    id: "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f",
    kind: "finance.expenses.bill-approval",
    kindTitle: "Bill approval",
    subjectType: "finance.expenses.bills",
    subjectId: "019a8f2e-7c41-7b3d-9e5f-6a7b8c9d0e1f",
    subjectName: "BL-0042 from Acme",
    requestedForId: asha,
    requestedForName: "Asha Rao",
    submittedById: asha,
    submittedByName: "Asha Rao",
    submittedAt,
    state: "submitted",
    waitingStepTitle: "Manager",
    waitingForKind: "person",
    waitingForId: viewer,
    waitingForName: "Ravi Kumar",
    waitingForRole: null,
    dueAt,
    late: false,
    closedAt: null,
    ...facts,
  };
}

test("requestRow_AWaitingRequest_SaysWhoAskedAndWhoItWaitsFor", () => {
  assert.deepEqual(requestRow(waitingRequest(), viewer), {
    id: "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f",
    kind: "finance.expenses.bill-approval",
    kindTitle: "Bill approval",
    subjectName: "BL-0042 from Acme",
    linkName: "Bill approval: BL-0042 from Acme, asked by Asha Rao on 23 Nov 2026, 10:00 am",
    askedBy: "Asha Rao",
    submittedBy: undefined,
    submittedAt,
    state: "submitted",
    waiting: "Manager step, waiting for you",
    dueAt,
    late: false,
    closedAt: undefined,
  });
});

test("requestRow_ARequestTheViewerAskedFor_IsAskedByYou", () => {
  const row = requestRow(waitingRequest({ requestedForId: viewer, submittedById: viewer }), viewer);

  assert.equal(row?.askedBy, "You");
  assert.equal(row?.submittedBy, undefined);
  assert.equal(row?.linkName, "Bill approval: BL-0042 from Acme, asked by you on 23 Nov 2026, 10:00 am");
});

test("requestRow_ARequestSubmittedForSomeoneElse_NamesTheSubmitter", () => {
  const row = requestRow(waitingRequest({ submittedById: ravi, submittedByName: "Ravi Kumar" }), viewer);

  assert.equal(row?.askedBy, "Asha Rao");
  assert.equal(row?.submittedBy, "Ravi Kumar");
});

test("requestRow_ARequestAJobSubmitted_NamesTheErpAsItsSubmitter", () => {
  const row = requestRow(
    waitingRequest({
      submittedById: "00000000-0000-0000-0000-000000000001",
      submittedByKind: "system",
      submittedByName: null,
    }),
    viewer,
  );

  assert.equal(row?.submittedBy, "the ERP");
});

test("kindFilterNote_KindsThatCouldNotBeReadAndNoKindChosen_ExplainsTheMissingFilter", () => {
  assert.equal(kindFilterNote(true, []), kindsUnavailableNote);
});

test("kindFilterNote_AKindChosenOrTheKindsRead_NeedsNoNote", () => {
  assert.equal(kindFilterNote(true, ["test.approvals.manager-01"]), undefined);
  assert.equal(kindFilterNote(false, []), undefined);
});

test("requestRow_AStepThatWaitsForARole_NamesTheRole", () => {
  const row = requestRow(
    waitingRequest({
      waitingStepTitle: "Finance",
      waitingForKind: "role",
      waitingForId: null,
      waitingForName: null,
      waitingForRole: "finance",
    }),
    viewer,
  );

  assert.equal(row?.waiting, "Finance step, waiting for Finance");
});

test("requestRow_AManagerStepWithNobodyLeft_WaitsForTheAdministrators", () => {
  const row = requestRow(
    waitingRequest({
      waitingForKind: "role",
      waitingForId: null,
      waitingForName: null,
      waitingForRole: "administrator",
    }),
    viewer,
  );

  assert.equal(row?.waiting, "Manager step, waiting for the administrators");
});

test("requestRow_ALateRequest_IsMarkedLate", () => {
  assert.equal(requestRow(waitingRequest({ late: true }), viewer)?.late, true);
});

test("requestRow_AClosedRequest_WaitsForNobodyAndIsNeverLate", () => {
  const row = requestRow(
    waitingRequest({
      state: "approved",
      waitingStepTitle: null,
      waitingForKind: null,
      waitingForId: null,
      waitingForName: null,
      dueAt: null,
      late: false,
      closedAt,
    }),
    viewer,
  );

  assert.equal(row?.state, "approved");
  assert.equal(row?.waiting, undefined);
  assert.equal(row?.dueAt, undefined);
  assert.equal(row?.late, false);
  assert.equal(row?.closedAt, closedAt);
});

test("requestRow_AKindNoReleaseDeclares_IsTitledByItsKey", () => {
  assert.equal(
    requestRow(waitingRequest({ kindTitle: "" }), viewer)?.kindTitle,
    "finance.expenses.bill-approval",
  );
});

test("requestRow_ARecordItsModuleNamesNot_HasNoName", () => {
  assert.equal(requestRow(waitingRequest({ subjectName: null }), viewer)?.subjectName, undefined);
});

test("requestRow_AnAnswerWithoutAMemberTheContractRequires_IsLeftOut", () => {
  assert.equal(requestRow(waitingRequest({ id: null }), viewer), undefined);
  assert.equal(requestRow(waitingRequest({ state: null }), viewer), undefined);
  assert.equal(requestRow(waitingRequest({ submittedAt: null }), viewer), undefined);
  assert.equal(requestRow(waitingRequest({ requestedForId: null }), viewer), undefined);
});

test("requestRows_SomeUnreadable_KeepsTheReadableOnesInOrder", () => {
  const rows = requestRows(
    [waitingRequest({ id: "first" }), waitingRequest({ kind: null }), waitingRequest({ id: "third" })],
    viewer,
  );

  assert.deepEqual(
    rows.map((row) => row.id),
    ["first", "third"],
  );
});

test("waitingStepOf_AClosedRequestWithStepFieldsLeft_WaitsAtNoStep", () => {
  assert.equal(waitingStepOf(waitingRequest({ state: "withdrawn" })), undefined);
  assert.deepEqual(waitingStepOf(waitingRequest()), {
    title: "Manager",
    kind: "person",
    id: viewer,
    name: "Ravi Kumar",
    role: undefined,
  });
});

test("nonEmpty_BlankText_IsNone", () => {
  assert.equal(nonEmpty("  "), undefined);
  assert.equal(nonEmpty(null), undefined);
  assert.equal(nonEmpty(" Asha Rao "), "Asha Rao");
});
