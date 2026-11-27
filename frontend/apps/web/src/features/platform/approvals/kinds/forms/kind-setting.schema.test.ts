import assert from "node:assert/strict";
import { test } from "node:test";

import type { ApprovalKindItem } from "../components/kind-items.ts";

import {
  changeKindSettingSchema,
  changeKindSettingValues,
  kindSettingOutOfDateCodes,
  maxWorkingDays,
  minWorkingDays,
  setUpKindSettingSchema,
  setUpKindSettingValues,
} from "./kind-setting.schema.ts";

const settingId = "019a8f2e-7c41-7b3d-9e5f-0a1b2c3d4e5f";

const daysMessage = "workingDaysToDecide: Give each step 1 to 30 working days.";

const billApproval: ApprovalKindItem = {
  key: "finance.expenses.bill-approval",
  title: "Bill approval",
  description: "A vendor bill waits for approval before it is posted to the books.",
  steps: [{ order: 1, title: "Manager", rule: "manager", role: undefined }],
  declaredWorkingDays: 2,
  setting: null,
};

function setUpMessages(values: Record<string, unknown>): string[] {
  const parsed = setUpKindSettingSchema.safeParse(values);
  return parsed.success
    ? []
    : parsed.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("setUpKindSettingValues_AKindNobodySetUp_StartsFromWhatItDeclares", () => {
  assert.deepEqual(setUpKindSettingValues(billApproval), {
    kind: "finance.expenses.bill-approval",
    required: true,
    workingDaysToDecide: "2",
  });
});

test("changeKindSettingValues_AKindWithASetting_StartsFromTheSetting", () => {
  assert.deepEqual(
    changeKindSettingValues({
      ...billApproval,
      setting: { id: settingId, required: false, workingDaysToDecide: 5, version: "AAAAAAAAB9E=" },
    }),
    { id: settingId, version: "AAAAAAAAB9E=", required: false, workingDaysToDecide: "5" },
  );
});

test("changeKindSettingValues_AKindWithoutASetting_HasNothingToChange", () => {
  assert.throws(() => changeKindSettingValues(billApproval), RangeError);
});

test("setUpKindSettingSchema_TheFewestAndMostWorkingDays_AreAccepted", () => {
  for (const days of [minWorkingDays, maxWorkingDays]) {
    assert.deepEqual(
      setUpKindSettingSchema.parse({
        kind: billApproval.key,
        required: true,
        workingDaysToDecide: ` ${days} `,
      }),
      {
        kind: billApproval.key,
        required: true,
        workingDaysToDecide: days,
      },
    );
  }
});

test("setUpKindSettingSchema_WorkingDaysOutsideOneToThirty_AreRefused", () => {
  for (const days of ["0", "31", "", "two", "2.5", "-1", "1e1"]) {
    assert.deepEqual(
      setUpMessages({ kind: billApproval.key, required: false, workingDaysToDecide: days }),
      [daysMessage],
      days,
    );
  }
});

test("setUpKindSettingSchema_AKindThePageDidNotRead_IsOutOfDate", () => {
  assert.deepEqual(setUpMessages({ kind: "", required: true, workingDaysToDecide: "2" }), [
    "kind: This page is out of date. Reload the page, then send the form again.",
  ]);
});

test("changeKindSettingSchema_ASetting_IsSentWithItsVersion", () => {
  assert.deepEqual(
    changeKindSettingSchema.parse({
      id: settingId,
      version: "AAAAAAAAB9E=",
      required: false,
      workingDaysToDecide: "7",
    }),
    { id: settingId, version: "AAAAAAAAB9E=", required: false, workingDaysToDecide: 7 },
  );
});

test("changeKindSettingSchema_ASettingThePageDidNotRead_IsOutOfDate", () => {
  const parsed = changeKindSettingSchema.safeParse({
    id: "x",
    version: "",
    required: "yes",
    workingDaysToDecide: "7",
  });

  assert.equal(parsed.success, false);
  assert.deepEqual(parsed.success ? [] : parsed.error.issues.map((issue) => issue.path.join(".")), [
    "id",
    "version",
    "required",
  ]);
});

test("kindSettingOutOfDateCodes_EveryWordedRefusal_OffersTheLatestSettings", () => {
  assert.deepEqual([...kindSettingOutOfDateCodes].sort(), [
    "approval.setting-exists",
    "approval.setting-not-found",
    "concurrency.conflict",
  ]);
});
