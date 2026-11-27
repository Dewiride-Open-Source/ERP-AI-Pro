import assert from "node:assert/strict";
import { test } from "node:test";

import type { ApprovalKindResponse } from "@dewiride/erp-api-client";

import { approvalKindOf, requiresApproval, UnreadableAnswerError, workingDaysInForce } from "./kind-items.ts";

const settingId = "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f";

function billApproval(values: Partial<ApprovalKindResponse> = {}): ApprovalKindResponse {
  return {
    kind: "finance.expenses.bill-approval",
    title: "Bill approval",
    description: "A vendor bill waits for approval before it is posted to the books.",
    steps: [
      { order: 1, title: "Manager", rule: "manager", role: null },
      { order: 2, title: "Finance", rule: "role", role: "finance" },
    ],
    declaredWorkingDays: 2,
    setting: null,
    ...values,
  };
}

test("approvalKindOf_AKindNobodySetUp_KeepsItsDeclarationWithoutASetting", () => {
  assert.deepEqual(approvalKindOf(billApproval()), {
    key: "finance.expenses.bill-approval",
    title: "Bill approval",
    description: "A vendor bill waits for approval before it is posted to the books.",
    steps: [
      { order: 1, title: "Manager", rule: "manager", role: undefined },
      { order: 2, title: "Finance", rule: "role", role: "finance" },
    ],
    declaredWorkingDays: 2,
    setting: null,
  });
});

test("approvalKindOf_AKindWithASetting_KeepsTheSetting", () => {
  const kind = approvalKindOf(
    billApproval({
      setting: { id: settingId, required: false, workingDaysToDecide: 5, version: "AAAAAAAAB9E=" },
    }),
  );

  assert.deepEqual(kind.setting, {
    id: settingId,
    required: false,
    workingDaysToDecide: 5,
    version: "AAAAAAAAB9E=",
  });
});

test("approvalKindOf_ANullSettingReadAsAnObjectWithoutMembers_IsNoSetting", () => {
  assert.equal(approvalKindOf(billApproval({ setting: {} })).setting, null);
});

test("approvalKindOf_AnAnswerWithoutAMemberTheContractRequires_IsUnreadable", () => {
  assert.throws(() => approvalKindOf(billApproval({ kind: null })), UnreadableAnswerError);
  assert.throws(() => approvalKindOf(billApproval({ title: " " })), UnreadableAnswerError);
  assert.throws(() => approvalKindOf(billApproval({ steps: null })), UnreadableAnswerError);
  assert.throws(() => approvalKindOf(billApproval({ declaredWorkingDays: null })), UnreadableAnswerError);
  assert.throws(
    () => approvalKindOf(billApproval({ steps: [{ order: 1, title: "Finance", rule: "role", role: null }] })),
    UnreadableAnswerError,
  );
  assert.throws(
    () =>
      approvalKindOf(billApproval({ setting: { id: settingId, required: true, version: "AAAAAAAAB9E=" } })),
    UnreadableAnswerError,
  );
});

test("requiresApproval_AKindNobodySetUp_NeedsApproval", () => {
  assert.equal(requiresApproval(approvalKindOf(billApproval())), true);
});

test("requiresApproval_AKindSwitchedOff_NeedsNone", () => {
  const kind = approvalKindOf(
    billApproval({
      setting: { id: settingId, required: false, workingDaysToDecide: 2, version: "AAAAAAAAB9E=" },
    }),
  );

  assert.equal(requiresApproval(kind), false);
});

test("workingDaysInForce_ASettingOrNone_IsTheSettingsDaysElseTheDeclaredOnes", () => {
  assert.equal(workingDaysInForce(approvalKindOf(billApproval())), 2);
  assert.equal(
    workingDaysInForce(
      approvalKindOf(
        billApproval({
          setting: { id: settingId, required: true, workingDaysToDecide: 7, version: "AAAAAAAAB9E=" },
        }),
      ),
    ),
    7,
  );
});
