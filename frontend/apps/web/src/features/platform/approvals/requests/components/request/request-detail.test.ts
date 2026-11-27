import assert from "node:assert/strict";
import { test } from "node:test";

import type { ApprovalRequestResponse, ApprovalStepResponse } from "@dewiride/erp-api-client";

import { actionSubjectOf, requestDetailOf } from "./request-detail.ts";

const viewer = "6f2c1d7e-9a41-4b8e-9c55-0d2f7a1b3c4e";

const asha = "1b7e4f20-3c5d-4e6f-8a9b-0c1d2e3f4a5b";

const submittedAt = new Date("2026-11-23T04:30:00Z");

const decidedAt = new Date("2026-11-24T06:15:00Z");

const dueAt = new Date("2026-11-26T18:30:00Z");

const managerStep: ApprovalStepResponse = {
  order: 1,
  title: "Manager",
  rule: "manager",
  state: "approved",
  approverKind: "person",
  approverId: viewer,
  approverName: "Ravi Kumar",
  approverRole: null,
  workingDays: 2,
  startedAt: submittedAt,
  dueAt: new Date("2026-11-25T18:30:00Z"),
  late: false,
  decidedById: viewer,
  decidedByName: "Ravi Kumar",
  decidedAt,
  inPlaceOfApprover: false,
  comment: "  Within the budget.  ",
};

const financeStep: ApprovalStepResponse = {
  order: 2,
  title: "Finance",
  rule: "role",
  state: "waiting",
  approverKind: "role",
  approverId: null,
  approverName: null,
  approverRole: "finance",
  workingDays: 2,
  startedAt: decidedAt,
  dueAt,
  late: true,
  decidedById: null,
  decidedByName: null,
  decidedAt: null,
  inPlaceOfApprover: false,
  comment: null,
};

function chainRequest(values: Partial<ApprovalRequestResponse> = {}): ApprovalRequestResponse {
  return {
    id: "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f",
    kind: "finance.expenses.bill-approval",
    kindTitle: "Bill approval",
    subjectType: "finance.expenses.bills",
    subjectId: "019a8f2e-7c41-7b3d-9e5f-6a7b8c9d0e1f",
    subjectName: null,
    requestedForId: asha,
    requestedForName: "Asha Rao",
    submittedById: asha,
    submittedByName: "Asha Rao",
    submittedAt,
    state: "submitted",
    waitingStepTitle: "Finance",
    waitingForKind: "role",
    waitingForId: null,
    waitingForName: null,
    waitingForRole: "finance",
    dueAt,
    late: true,
    closedAt: null,
    version: "AAAAAAAAB9E=",
    steps: [financeStep, managerStep],
    earlierRequests: [
      {
        id: "019a8f2e-7c41-7b3d-9e5f-1a2b3c4d5e6f",
        state: "returned",
        submittedAt: new Date("2026-11-20T04:30:00Z"),
        closedAt: new Date("2026-11-20T09:00:00Z"),
      },
    ],
    canDecide: true,
    decidesInPlace: true,
    canWithdraw: false,
    ...values,
  };
}

test("requestDetailOf_ARequest_ListsItsStepsInTheirOrder", () => {
  const detail = requestDetailOf(chainRequest(), viewer);

  assert.deepEqual(
    detail?.steps.map((step) => [step.order, step.title, step.state]),
    [
      [1, "Manager", "approved"],
      [2, "Finance", "waiting"],
    ],
  );
});

test("requestDetailOf_ADecidedStep_NamesItsDeciderAndKeepsTheirCommentTrimmed", () => {
  const manager = requestDetailOf(chainRequest(), viewer)?.steps[0];

  assert.equal(manager?.approver, "you");
  assert.equal(manager?.decidedBy, "you");
  assert.equal(manager?.decidedAt, decidedAt);
  assert.equal(manager?.comment, "Within the budget.");
});

test("requestDetailOf_ARoleStep_WaitsForTheRolesHolders", () => {
  const finance = requestDetailOf(chainRequest(), viewer)?.steps[1];

  assert.equal(finance?.approver, "Finance");
  assert.equal(finance?.decidedBy, undefined);
  assert.equal(finance?.late, true);
  assert.equal(finance?.dueAt, dueAt);
});

test("requestDetailOf_WhatTheReaderMayDo_IsWhatTheApiSays", () => {
  const detail = requestDetailOf(chainRequest(), viewer);

  assert.equal(detail?.canDecide, true);
  assert.equal(detail?.decidesInPlace, true);
  assert.equal(detail?.canWithdraw, false);
  assert.equal(detail?.waitingApprover, "Finance");
});

test("requestDetailOf_EarlierRequests_AreKept", () => {
  assert.deepEqual(requestDetailOf(chainRequest(), viewer)?.earlier, [
    {
      id: "019a8f2e-7c41-7b3d-9e5f-1a2b3c4d5e6f",
      state: "returned",
      submittedAt: new Date("2026-11-20T04:30:00Z"),
      closedAt: new Date("2026-11-20T09:00:00Z"),
    },
  ]);
});

test("requestDetailOf_AnAnswerWithoutItsVersionOrSteps_IsUnreadable", () => {
  assert.equal(requestDetailOf(chainRequest({ version: null }), viewer), undefined);
  assert.equal(requestDetailOf(chainRequest({ steps: [] }), viewer), undefined);
  assert.equal(requestDetailOf(chainRequest({ id: null }), viewer), undefined);
});

test("actionSubjectOf_ARequestWaitingAtItsLastStep_HasNoNextStep", () => {
  const detail = requestDetailOf(chainRequest(), viewer);

  assert.deepEqual(detail === undefined ? undefined : actionSubjectOf(detail), {
    id: "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f",
    version: "AAAAAAAAB9E=",
    askedBy: "Asha Rao",
    waitingStepTitle: "Finance",
    nextStepTitle: undefined,
    inPlaceOf: "Finance",
  });
});

test("actionSubjectOf_ARequestWaitingAtItsFirstStep_NamesTheNextStep", () => {
  const detail = requestDetailOf(
    chainRequest({
      steps: [
        {
          ...managerStep,
          state: "waiting",
          decidedById: null,
          decidedByName: null,
          decidedAt: null,
          comment: null,
        },
        { ...financeStep, state: "pending", startedAt: null, dueAt: null, late: false },
      ],
      waitingStepTitle: "Manager",
      waitingForKind: "person",
      waitingForId: viewer,
      waitingForName: "Ravi Kumar",
      waitingForRole: null,
      decidesInPlace: false,
    }),
    viewer,
  );
  const subject = detail === undefined ? undefined : actionSubjectOf(detail);

  assert.equal(subject?.waitingStepTitle, "Manager");
  assert.equal(subject?.nextStepTitle, "Finance");
  assert.equal(subject?.inPlaceOf, undefined);
});
