import assert from "node:assert/strict";
import { test } from "node:test";

import type { ApprovalState, ApprovalStepState, ErpRole } from "@dewiride/erp-api-client";

import {
  approvalStateLabel,
  approverText,
  closedOnText,
  dueByText,
  dueDateText,
  dueSentence,
  lastDayToDecide,
  mineCountText,
  personText,
  requestLinkName,
  roleGroupText,
  sameId,
  stepCommentLabel,
  stepDaysText,
  stepSentence,
  stepStateLabel,
  systemActorId,
  waitingCountText,
  waitingText,
  workingDaysText,
  type StepFacts,
} from "./request-text.ts";

const viewer = "6f2c1d7e-9a41-4b8e-9c55-0d2f7a1b3c4e";

const asha = { id: "1b7e4f20-3c5d-4e6f-8a9b-0c1d2e3f4a5b", name: "Asha Rao" };

// Midnight in India that ends Wednesday, 25 November 2026, the first instant a step due that day is late.
const thursdayMidnight = new Date("2026-11-25T18:30:00Z");

const decidedAt = new Date("2026-11-24T06:15:00Z");

const startedAt = new Date("2026-11-23T04:30:00Z");

function step(state: ApprovalStepState, facts: Partial<StepFacts> = {}): StepFacts {
  return {
    state,
    approver: "Asha Rao",
    decidedBy: undefined,
    decidedAt: undefined,
    startedAt: undefined,
    inPlaceOfApprover: false,
    ...facts,
  };
}

test("approvalStateLabel_EveryState_HasTheWordsThePageShows", () => {
  const labels: Record<ApprovalState, string> = {
    submitted: "Waiting",
    approved: "Approved",
    returned: "Sent back",
    rejected: "Rejected",
    withdrawn: "Withdrawn",
  };
  for (const [state, label] of Object.entries(labels)) {
    assert.equal(approvalStateLabel(state as ApprovalState), label);
  }
});

test("stepStateLabel_EveryStepState_HasTheWordsThePageShows", () => {
  const labels: Record<ApprovalStepState, string> = {
    pending: "Not reached yet",
    waiting: "Waiting",
    approved: "Approved",
    returned: "Sent back",
    rejected: "Rejected",
    closed: "Not decided",
  };
  for (const [state, label] of Object.entries(labels)) {
    assert.equal(stepStateLabel(state as ApprovalStepState), label);
  }
});

test("roleGroupText_EveryRole_NamesThePeopleWhoHoldIt", () => {
  const groups: Record<ErpRole, readonly [string, string]> = {
    employee: ["the employees", "The employees"],
    finance: ["Finance", "Finance"],
    hr: ["HR", "HR"],
    manager: ["the managers", "The managers"],
    administrator: ["the administrators", "The administrators"],
  };
  for (const [role, [within, start]] of Object.entries(groups)) {
    assert.equal(roleGroupText(role as ErpRole), within);
    assert.equal(roleGroupText(role as ErpRole, true), start);
  }
});

test("sameId_IdsInDifferentCase_AreTheSame", () => {
  assert.equal(sameId(viewer, viewer.toUpperCase()), true);
  assert.equal(sameId(viewer, asha.id), false);
});

test("personText_TheViewer_IsYou", () => {
  assert.equal(personText({ id: viewer.toUpperCase(), name: "Ravi Kumar" }, viewer), "you");
  assert.equal(personText({ id: viewer, name: "Ravi Kumar" }, viewer, true), "You");
});

test("personText_TheErpsOwnActor_IsTheErp", () => {
  assert.equal(personText({ id: systemActorId, name: undefined }, viewer), "the ERP");
  assert.equal(personText({ id: systemActorId, name: undefined }, viewer, true), "The ERP");
});

test("personText_SomeoneElse_IsTheirNameAsTheirRecordSpellsIt", () => {
  assert.equal(personText(asha, viewer), "Asha Rao");
  assert.equal(personText({ id: asha.id, name: "asha rao" }, viewer, true), "asha rao");
  assert.equal(personText(asha, undefined), "Asha Rao");
});

test("personText_APersonTheErpKnowsNoNameFor_IsAnUnknownPerson", () => {
  assert.equal(personText({ id: asha.id, name: undefined }, viewer), "an unknown person");
  assert.equal(personText({ id: undefined, name: undefined }, viewer, true), "An unknown person");
});

test("approverText_ARole_NamesItsHoldersAndAPerson_TheirName", () => {
  assert.equal(
    approverText({ kind: "role", role: "finance", id: undefined, name: undefined }, viewer),
    "Finance",
  );
  assert.equal(
    approverText({ kind: "role", role: "administrator", id: undefined, name: undefined }, viewer, true),
    "The administrators",
  );
  assert.equal(approverText({ kind: "person", role: undefined, ...asha }, viewer), "Asha Rao");
  assert.equal(approverText({ kind: "person", role: undefined, id: viewer, name: "Ravi" }, viewer), "you");
});

test("lastDayToDecide_TheMidnightThatMakesAStepLate_IsTheLastInstantOfTheDayBefore", () => {
  assert.equal(lastDayToDecide(thursdayMidnight).toISOString(), "2026-11-25T18:29:59.999Z");
});

test("dueDateText_ADueTime_IsTheLastDayInIndia", () => {
  assert.equal(dueDateText(thursdayMidnight), "25 Nov 2026");
  assert.equal(dueDateText(new Date("2026-11-25T18:30:00.001Z")), "26 Nov 2026");
});

test("dueSentence_BeforeAndAfterTheDueTime_SaysWhenTheStepIsDue", () => {
  assert.equal(dueSentence(thursdayMidnight, false), "Due by the end of Wednesday, 25 November 2026.");
  assert.equal(dueSentence(thursdayMidnight, true), "Was due by the end of Wednesday, 25 November 2026.");
});

test("dueByText_ADueTime_IsTheEndOfTheLastDay", () => {
  assert.equal(dueByText(thursdayMidnight), "By the end of Wednesday, 25 November 2026");
});

test("stepDaysText_APendingStep_SaysItsWorkingDays", () => {
  assert.equal(stepDaysText(2), "2 working days to decide, once it starts waiting.");
  assert.equal(stepDaysText(1), "1 working day to decide, once it starts waiting.");
});

test("dueSentence_ADueTimeThatEndsAFinancialYear_IsThe31stOfMarch", () => {
  assert.equal(
    dueSentence(new Date("2027-03-31T18:30:00Z"), false),
    "Due by the end of Wednesday, 31 March 2027.",
  );
});

test("waitingText_AWaitingStep_NamesTheStepAndWhoItWaitsFor", () => {
  assert.equal(waitingText("Manager", "you"), "Manager step, waiting for you");
  assert.equal(waitingText("Finance", "Finance"), "Finance step, waiting for Finance");
});

test("requestLinkName_ARequest_NamesItsRecordWhoAskedAndWhen", () => {
  assert.equal(
    requestLinkName("Bill approval", "BL-0042 from Acme", "Asha Rao", startedAt),
    "Bill approval: BL-0042 from Acme, asked by Asha Rao on 23 Nov 2026, 10:00 am",
  );
  assert.equal(
    requestLinkName("Bill approval", undefined, "you", startedAt),
    "Bill approval, asked by you on 23 Nov 2026, 10:00 am",
  );
});

test("closedOnText_AClosingTime_IsTheDateAndTimeInIndia", () => {
  assert.equal(closedOnText(decidedAt), "on 24 Nov 2026, 11:45 am");
});

test("workingDaysText_OneAndMore_CountsTheWorkingDays", () => {
  assert.equal(workingDaysText(1), "1 working day");
  assert.equal(workingDaysText(2), "2 working days");
  assert.equal(workingDaysText(30), "30 working days");
});

test("waitingCountText_NothingWaiting_SaysNothing", () => {
  assert.equal(waitingCountText({ waitingForMe: 0, lateForMe: 0, mineWaiting: 3 }), undefined);
});

test("waitingCountText_OneRequest_SaysWhetherItIsLate", () => {
  assert.equal(
    waitingCountText({ waitingForMe: 1, lateForMe: 0, mineWaiting: 0 }),
    "1 request waits for your decision.",
  );
  assert.equal(
    waitingCountText({ waitingForMe: 1, lateForMe: 1, mineWaiting: 0 }),
    "1 request waits for your decision, and it is late.",
  );
});

test("waitingCountText_SeveralRequests_SaysHowManyAreLate", () => {
  assert.equal(
    waitingCountText({ waitingForMe: 3, lateForMe: 0, mineWaiting: 0 }),
    "3 requests wait for your decision.",
  );
  assert.equal(
    waitingCountText({ waitingForMe: 3, lateForMe: 1, mineWaiting: 0 }),
    "3 requests wait for your decision, 1 of them late.",
  );
  assert.equal(
    waitingCountText({ waitingForMe: 3, lateForMe: 3, mineWaiting: 0 }),
    "3 requests wait for your decision, and all of them are late.",
  );
});

test("waitingCountText_ALakhOfRequests_GroupsTheCountTheIndianWay", () => {
  assert.equal(
    waitingCountText({ waitingForMe: 100000, lateForMe: 12000, mineWaiting: 0 }),
    "1,00,000 requests wait for your decision, 12,000 of them late.",
  );
});

test("mineCountText_TheRequestsStillWaiting_AreCounted", () => {
  assert.equal(mineCountText({ waitingForMe: 4, lateForMe: 1, mineWaiting: 0 }), undefined);
  assert.equal(
    mineCountText({ waitingForMe: 0, lateForMe: 0, mineWaiting: 1 }),
    "1 of your requests still waits for a decision.",
  );
  assert.equal(
    mineCountText({ waitingForMe: 0, lateForMe: 0, mineWaiting: 2 }),
    "2 of your requests still wait for a decision.",
  );
});

test("stepSentence_APendingStep_SaysWhoItWillWaitFor", () => {
  assert.equal(
    stepSentence(step("pending"), "submitted"),
    "Waits for Asha Rao once the steps before it are approved.",
  );
});

test("stepSentence_AWaitingStep_SaysSinceWhen", () => {
  assert.equal(
    stepSentence(step("waiting", { startedAt }), "submitted"),
    "Waiting for Asha Rao since 23 Nov 2026, 10:00 am.",
  );
  assert.equal(stepSentence(step("waiting"), "submitted"), "Waiting for Asha Rao.");
});

test("stepSentence_ADecidedStep_NamesWhoDecidedItAndWhen", () => {
  const decided = { decidedBy: "you", decidedAt };

  assert.equal(
    stepSentence(step("approved", decided), "submitted"),
    "Approved by you on 24 Nov 2026, 11:45 am.",
  );
  assert.equal(
    stepSentence(step("returned", decided), "returned"),
    "Sent back by you on 24 Nov 2026, 11:45 am.",
  );
  assert.equal(
    stepSentence(step("rejected", decided), "rejected"),
    "Rejected by you on 24 Nov 2026, 11:45 am.",
  );
});

test("stepSentence_AStepDecidedInPlaceOfItsApprover_SaysWhoseDecisionItTook", () => {
  assert.equal(
    stepSentence(
      step("approved", { decidedBy: "Ravi Kumar", decidedAt, inPlaceOfApprover: true, approver: "Finance" }),
      "approved",
    ),
    "Approved by Ravi Kumar on 24 Nov 2026, 11:45 am, in place of Finance.",
  );
});

test("stepSentence_AStepClosedWithoutADecision_SaysWhy", () => {
  assert.equal(stepSentence(step("closed"), "returned"), "Not decided: an earlier step closed the request.");
  assert.equal(
    stepSentence(step("closed"), "withdrawn"),
    "Not decided: the request was withdrawn before it reached this step.",
  );
  assert.equal(
    stepSentence(step("closed", { startedAt }), "withdrawn"),
    "Not decided: the request was withdrawn while this step waited for Asha Rao.",
  );
});

test("stepCommentLabel_EachDecision_NamesWhatItsCommentSays", () => {
  assert.equal(stepCommentLabel("approved"), "Note");
  assert.equal(stepCommentLabel("returned"), "What needs to change");
  assert.equal(stepCommentLabel("rejected"), "Why it is rejected");
  assert.equal(stepCommentLabel("closed"), "Why it was withdrawn");
});
