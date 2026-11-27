import assert from "node:assert/strict";
import { test } from "node:test";

import type { ApprovalKindItem } from "./kind-items.ts";
import {
  approvalText,
  changeKindLabel,
  settingSavedMessage,
  stepRuleText,
  workingDaysSentence,
} from "./kind-text.ts";

const settingId = "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f";

const billApproval: ApprovalKindItem = {
  key: "finance.expenses.bill-approval",
  title: "Bill approval",
  description: "A vendor bill waits for approval before it is posted to the books.",
  steps: [
    { order: 1, title: "Manager", rule: "manager", role: undefined },
    { order: 2, title: "Finance", rule: "role", role: "finance" },
  ],
  declaredWorkingDays: 2,
  setting: null,
};

test("stepRuleText_AManagerStep_SaysWhoseManagerDecidesIt", () => {
  assert.equal(
    stepRuleText({ order: 1, title: "Manager", rule: "manager", role: undefined }),
    "Decided by the manager of the person who asks, as on the day they ask. A manager who has left or has never signed in is passed over for the one above, and the administrators decide when nobody is left.",
  );
});

test("stepRuleText_ARoleStep_NamesTheRolesHolders", () => {
  assert.equal(
    stepRuleText({ order: 2, title: "Finance", rule: "role", role: "finance" }),
    "Decided by Finance.",
  );
  assert.equal(
    stepRuleText({ order: 1, title: "Administrator", rule: "role", role: "administrator" }),
    "Decided by the administrators.",
  );
});

test("approvalText_AKindThatNeedsApproval_SaysItsStepsAreFollowed", () => {
  assert.equal(approvalText(billApproval), "Each request goes through these steps, in this order.");
});

test("approvalText_AKindSwitchedOff_SaysItsRecordsGoThroughWithoutARequest", () => {
  assert.equal(
    approvalText({
      ...billApproval,
      setting: { id: settingId, required: false, workingDaysToDecide: 2, version: "AAAAAAAAB9E=" },
    }),
    "Records of this kind go through at once, without a request, so nobody but the person who made one looks at it. Once approval is switched on again, each request goes through these steps.",
  );
});

test("workingDaysSentence_TheDeclaredDays_AreTheStepsDays", () => {
  assert.equal(workingDaysSentence(billApproval), "2 working days for each step.");
  assert.equal(
    workingDaysSentence({
      ...billApproval,
      setting: { id: settingId, required: true, workingDaysToDecide: 2, version: "AAAAAAAAB9E=" },
    }),
    "2 working days for each step.",
  );
});

test("workingDaysSentence_DaysAnAdministratorChanged_SayTheErpsOwnNumberToo", () => {
  assert.equal(
    workingDaysSentence({
      ...billApproval,
      setting: { id: settingId, required: true, workingDaysToDecide: 1, version: "AAAAAAAAB9E=" },
    }),
    "1 working day for each step, as an administrator set it; the ERP's own number is 2.",
  );
});

test("changeKindLabel_AKind_NamesIt", () => {
  assert.equal(changeKindLabel("Bill approval"), "Change Bill approval");
});

test("settingSavedMessage_OnOrOff_SaysWhatTheKindNowDoes", () => {
  assert.equal(
    settingSavedMessage("Bill approval", true, 3),
    "Saved Bill approval: each step of a new request has 3 working days.",
  );
  assert.equal(
    settingSavedMessage("Bill approval", false, 3),
    "Saved Bill approval: its records now go through without a request.",
  );
});
