import assert from "node:assert/strict";
import { test } from "node:test";

import { ownRecordInactive } from "../../details/components/person-action-text.ts";
import { organisationBusy } from "../../positions/forms/record-move.schema.ts";

import {
  accountCheckFailure,
  accountCheckResult,
  accountCheckSummary,
  checkTimedOut,
  deactivationReasonLabel,
  microsoftAccessDenied,
  microsoftUnavailable,
  type AccountCheckResult,
} from "./account-check.ts";

const asha = { id: "0199a3f2-7c1e-7b4a-9d2e-000000000001", displayName: "Asha Rao" };

const kiran = { id: "0199a3f2-7c1e-7b4a-9d2e-000000000002", displayName: "Kiran Das" };

function checked(
  checkedCount: number,
  deactivated: AccountCheckResult["deactivated"] = [],
): AccountCheckResult {
  return { status: "checked", checkedCount, deactivated };
}

test("accountCheckResult_TheApisAnswer_KeepsTheCountAndEachDeactivatedPersonWithTheReason", () => {
  assert.deepEqual(
    accountCheckResult({
      checkedCount: 12,
      deactivated: [
        { ...asha, deactivatedBecause: "accountDisabled" },
        { ...kiran, deactivatedBecause: "accountDeleted" },
      ],
    }),
    checked(12, [
      { ...asha, reason: "accountDisabled" },
      { ...kiran, reason: "accountDeleted" },
    ]),
  );
});

test("accountCheckResult_PeopleWithoutAnIdANameOrAReason_AreLeftOut", () => {
  assert.deepEqual(
    accountCheckResult({
      checkedCount: 3,
      deactivated: [
        { id: null, displayName: "Asha Rao", deactivatedBecause: "accountDisabled" },
        { ...asha, displayName: "", deactivatedBecause: "accountDisabled" },
        { ...asha, deactivatedBecause: null },
        { ...kiran, deactivatedBecause: "accountDeleted" },
      ],
    }),
    checked(3, [{ ...kiran, reason: "accountDeleted" }]),
  );
});

test("accountCheckResult_AnswerWithoutItsMembers_CheckedNobody", () => {
  assert.deepEqual(accountCheckResult({}), checked(0));
});

test("accountCheckSummary_NobodyDeactivated_SaysEveryAccountIsActive", () => {
  assert.equal(accountCheckSummary(checked(1)), "Checked 1 person. Every account is active.");
  assert.equal(accountCheckSummary(checked(125000)), "Checked 1,25,000 people. Every account is active.");
});

test("accountCheckSummary_PeopleDeactivated_CountsThem", () => {
  assert.equal(
    accountCheckSummary(checked(12, [{ ...asha, reason: "accountDisabled" }])),
    "Checked 12 people. Deactivated 1 person whose Microsoft account is disabled or deleted.",
  );
  assert.equal(
    accountCheckSummary(
      checked(12, [
        { ...asha, reason: "accountDisabled" },
        { ...kiran, reason: "accountDeleted" },
      ]),
    ),
    "Checked 12 people. Deactivated 2 people whose Microsoft account is disabled or deleted.",
  );
});

test("accountCheckSummary_NobodyLinkedToAMicrosoftAccount_SaysThereWasNoOneToCheck", () => {
  assert.equal(
    accountCheckSummary(checked(0)),
    "No active person in the ERP is linked to a Microsoft account, so there was no one to check.",
  );
});

test("deactivationReasonLabel_EachReason_SaysWhatTheCheckFound", () => {
  assert.deepEqual(
    (["accountDisabled", "accountDeleted", "administrator"] as const).map(deactivationReasonLabel),
    ["Microsoft account disabled", "Microsoft account deleted", "Deactivated by an administrator"],
  );
});

test("accountCheckFailure_MicrosoftRefusesOrCannotAnswer_IsWordedForThePersonWithItsReference", () => {
  const failure = (status: number, code: string) =>
    accountCheckFailure({ status, problem: { status, code, traceId: "00-4bf9" } });

  assert.deepEqual(failure(403, "directory.access-denied"), {
    status: "failed",
    message: microsoftAccessDenied,
    reference: "00-4bf9",
  });
  assert.deepEqual(failure(503, "directory.unavailable"), {
    status: "failed",
    message: microsoftUnavailable,
    reference: "00-4bf9",
  });
  assert.deepEqual(failure(504, "request.timeout"), {
    status: "failed",
    message: checkTimedOut,
    reference: "00-4bf9",
  });
});

test("accountCheckFailure_TheAdministratorsRecordOrAnotherChangeStoppedTheCheck_SaysNothingChanged", () => {
  assert.equal(
    accountCheckFailure({ status: 409, problem: { status: 409, code: "user.actor-inactive" } })?.message,
    ownRecordInactive,
  );
  assert.equal(
    accountCheckFailure({ status: 409, problem: { status: 409, code: "concurrency.conflict" } })?.message,
    organisationBusy,
  );
});

test("accountCheckFailure_AnyOtherAnswer_IsLeftToTheFormsKit", () => {
  assert.equal(
    accountCheckFailure({ status: 403, problem: { status: 403, code: "request.forbidden" } }),
    undefined,
  );
  assert.equal(accountCheckFailure({ status: 503, problem: { status: 503 } }), undefined);
  assert.equal(
    accountCheckFailure({ status: 500, problem: { status: 500, code: "directory.unavailable" } }),
    undefined,
  );
});

test("accountCheckMessages_WhatThePersonReads_SaysNoOneWasCheckedAndWhatToDo", () => {
  assert.equal(
    microsoftAccessDenied,
    "Microsoft has not granted the ERP permission to read accounts, so no one was checked. Whoever looks after your organisation's Microsoft Entra needs to grant it.",
  );
  assert.equal(
    microsoftUnavailable,
    "Microsoft could not be reached or gave no usable answer, so no one was checked. Try again in a few minutes.",
  );
  assert.equal(checkTimedOut, "The check took too long and was stopped. Try again in a few minutes.");
});
