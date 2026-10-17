import assert from "node:assert/strict";
import { test } from "node:test";

import {
  deletion,
  deletionProblemMessages,
  isOwnRecord,
  pageOutOfDate,
  refusedOutcome,
  statusChange,
  statusProblemMessages,
} from "./person-action-text.ts";

test("statusChange_ActivePerson_OffersToDeactivateThemAsADestructiveStep", () => {
  assert.deepEqual(statusChange("active", "Meera Nair"), {
    to: "deactivated",
    action: "Deactivate",
    title: "Deactivate Meera Nair?",
    description:
      "Meera Nair can no longer sign in, and any session they have open ends at their next step in the ERP. You can reactivate them later.",
    confirm: "Deactivate",
    cancel: "Keep them active",
    done: "Deactivated Meera Nair.",
    tone: "destructive",
  });
});

test("statusChange_DeactivatedPerson_OffersToReactivateThem", () => {
  assert.deepEqual(statusChange("deactivated", "Meera Nair"), {
    to: "active",
    action: "Reactivate",
    title: "Reactivate Meera Nair?",
    description: "Meera Nair can sign in again with their Microsoft account.",
    confirm: "Reactivate",
    cancel: "Keep them deactivated",
    done: "Reactivated Meera Nair.",
    tone: "default",
  });
});

test("deletion_ActivePerson_AdvisesDeactivatingToKeepThemOut", () => {
  assert.deepEqual(deletion("Meera Nair", "active"), {
    title: "Delete Meera Nair?",
    description:
      "Meera Nair is removed from People, and any session they have open ends at their next step. If they can still sign in through Microsoft Entra, their next sign-in creates a new record, so deactivate them instead to keep them out. Their changes stay in the audit trail.",
    confirm: "Delete",
    cancel: "Keep the record",
    done: "Deleted Meera Nair.",
    gone: "Meera Nair is no longer registered.",
  });
});

test("deletion_DeactivatedPerson_SaysDeletingLetsTheirNextSignInCreateAnActiveRecord", () => {
  assert.deepEqual(deletion("Meera Nair", "deactivated"), {
    title: "Delete Meera Nair?",
    description:
      "Meera Nair is removed from People. Being deactivated is what keeps them out, and deleting the record ends that: if they can still sign in through Microsoft Entra, their next sign-in creates a new, active record. To keep them out, keep the record. Their changes stay in the audit trail.",
    confirm: "Delete",
    cancel: "Keep the record",
    done: "Deleted Meera Nair.",
    gone: "Meera Nair is no longer registered.",
  });
});

test("deletion_UnknownStatus_UsesTheWordingOfAnActivePerson", () => {
  assert.deepEqual(deletion("Meera Nair", undefined), deletion("Meera Nair", "active"));
});

test("isOwnRecord_SameGuidInAnyCase_IsTheSignedInPersonsRecord", () => {
  const id = "3f0c2a8e-6b1d-4c7e-9a52-0d8e4f1b7c31";

  assert.equal(isOwnRecord(id, id), true);
  assert.equal(isOwnRecord(id.toUpperCase(), id), true);
  assert.equal(isOwnRecord(id, "8b6e1f47-2c9a-4d3b-b5e0-7a19c4d2e865"), false);
  assert.equal(isOwnRecord(undefined, id), false);
  assert.equal(isOwnRecord(null, id), false);
  assert.equal(isOwnRecord(id, undefined), false);
});

test("refusedOutcome_FailedAction_KeepsItsMessageAndReference", () => {
  assert.deepEqual(
    refusedOutcome({
      status: "failed",
      message: "The server could not finish this. Try again.",
      reference: "4bf9",
    }),
    { done: false, message: "The server could not finish this. Try again.", reference: "4bf9" },
  );
});

test("refusedOutcome_RequestTheApiFoundMalformed_AsksForAReload", () => {
  assert.deepEqual(
    refusedOutcome({
      status: "invalid",
      fieldErrors: { version: ["The version is not one the API issued."] },
      formErrors: [],
    }),
    { done: false, message: pageOutOfDate },
  );
});

test("problemMessages_RefusalsOfTheOwnRecordAndAManager_AreWordedForThePerson", () => {
  assert.equal(
    statusProblemMessages["user.own-status"],
    "You cannot change the status of your own record. Another administrator can.",
  );
  assert.equal(
    deletionProblemMessages["user.own-deletion"],
    "You cannot delete your own record. Another administrator can.",
  );
  assert.equal(
    deletionProblemMessages["user.manages-people"],
    "People report to this person today or from a later date. Record another manager for them first.",
  );
});
