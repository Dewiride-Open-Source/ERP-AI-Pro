import assert from "node:assert/strict";
import { test } from "node:test";

import type { ApprovalRequestResponse } from "@dewiride/erp-api-client";

import { actionDescription, actionDoneMessage, actionTexts, inPlaceNotice } from "./decision-text.ts";
import type { RequestActionSubject } from "./request-detail.ts";

const viewer = "6f2c1d7e-9a41-4b8e-9c55-0d2f7a1b3c4e";

const subject: RequestActionSubject = {
  id: "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f",
  version: "AAAAAAAAB9E=",
  askedBy: "Asha Rao",
  waitingStepTitle: "Manager",
  nextStepTitle: "Finance",
  inPlaceOf: undefined,
};

function answer(values: Partial<ApprovalRequestResponse>): ApprovalRequestResponse {
  return {
    state: "submitted",
    steps: [
      { order: 1, title: "Manager", state: "approved" },
      { order: 2, title: "Finance", state: "waiting" },
    ],
    waitingStepTitle: "Finance",
    waitingForKind: "role",
    waitingForId: null,
    waitingForName: null,
    waitingForRole: "finance",
    ...values,
  };
}

test("actionTexts_EachAction_HasTheApprovedButtonAndReasonWords", () => {
  assert.deepEqual(
    Object.values(actionTexts).map((text) => text.button),
    ["Approve", "Send back", "Reject", "Withdraw"],
  );
  assert.equal(actionTexts.return.commentLabel, "What needs to change?");
  assert.equal(actionTexts.reject.commentLabel, "Why is it rejected?");
});

test("actionDescription_ApprovingAStepBeforeTheLast_SaysTheRequestMovesOn", () => {
  assert.equal(
    actionDescription("approve", subject),
    "Approving the Manager step sends the request on to its next step, Finance.",
  );
});

test("actionDescription_ApprovingTheLastStep_SaysTheRequestIsApproved", () => {
  assert.equal(
    actionDescription("approve", { ...subject, waitingStepTitle: "Finance", nextStepTitle: undefined }),
    "Approving the Finance step approves the request.",
  );
});

test("actionDescription_ASubjectWithoutAWaitingStep_SaysItDecidesTheStepTheRequestWaitsAt", () => {
  assert.equal(
    actionDescription("approve", { ...subject, waitingStepTitle: undefined, nextStepTitle: undefined }),
    "Approving it decides the step the request waits at.",
  );
});

test("actionDescription_ClosingTheRequest_SaysWhatHappensToIt", () => {
  assert.equal(
    actionDescription("return", subject),
    "Sending it back closes the request. Asha Rao can change the record and ask again.",
  );
  assert.equal(actionDescription("reject", subject), "Rejecting it closes the request without approving it.");
  assert.equal(
    actionDescription("withdraw", subject),
    "Withdrawing it closes the request, and nobody decides it.",
  );
});

test("inPlaceNotice_AnApprover_SaysWhoseDecisionTheReaderTakes", () => {
  assert.equal(
    inPlaceNotice("Asha Rao"),
    "You decide in place of Asha Rao. The request's history will say so.",
  );
});

test("actionDoneMessage_AStepApprovedBeforeTheLast_SaysWhoTheNextStepWaitsFor", () => {
  assert.equal(
    actionDoneMessage("approve", answer({}), viewer),
    "You approved the Manager step. Next, the Finance step waits for Finance.",
  );
});

test("actionDoneMessage_ApprovedStepsWithoutTitles_SaysAStepWasApproved", () => {
  assert.equal(
    actionDoneMessage(
      "approve",
      answer({
        steps: [
          { order: 1, state: "approved" },
          { order: 2, title: "Finance", state: "waiting" },
        ],
      }),
      viewer,
    ),
    "You approved a step. Next, the Finance step waits for Finance.",
  );
});

test("actionDoneMessage_TheNextStepWaitsForTheReader_SaysItWaitsForThem", () => {
  assert.equal(
    actionDoneMessage(
      "approve",
      answer({
        waitingForKind: "person",
        waitingForId: viewer,
        waitingForName: "Ravi Kumar",
        waitingForRole: null,
      }),
      viewer,
    ),
    "You approved the Manager step. Next, the Finance step waits for you.",
  );
});

test("actionDoneMessage_TheLastStepApproved_SaysTheRequestIsApproved", () => {
  assert.equal(
    actionDoneMessage(
      "approve",
      answer({ state: "approved", waitingStepTitle: null, waitingForKind: null }),
      viewer,
    ),
    "You approved the request.",
  );
});

test("actionDoneMessage_ClosingTheRequest_SaysWhatTheReaderDid", () => {
  assert.equal(
    actionDoneMessage("return", answer({ state: "returned" }), viewer),
    "You sent the request back.",
  );
  assert.equal(
    actionDoneMessage("reject", answer({ state: "rejected" }), viewer),
    "You rejected the request.",
  );
  assert.equal(
    actionDoneMessage("withdraw", answer({ state: "withdrawn" }), viewer),
    "You withdrew the request.",
  );
});
