import assert from "node:assert/strict";
import { test } from "node:test";

import { ApiError } from "@/shared/api/problem-details";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";

import { ownRecordInactive } from "../components/person-action-text.ts";

import {
  newManagerMissing,
  reportsToMissing,
  teamHandoverProblemMessages,
  teamHandoverSchema,
  teamHandoverValues,
} from "./team-handover.schema.ts";

const personId = "0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e";

const managerId = "0199a3f2-7c1e-7b4a-9d2e-000000000003";

const version = "AAAAAAAAB9E=";

function reported(values: unknown): string[] {
  const result = teamHandoverSchema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("teamHandoverSchema_StartingValues_AsksWhoTheTeamReportsTo", () => {
  assert.deepEqual(reported(teamHandoverValues(personId, version)), [`reportsTo: ${reportsToMissing}`]);
});

test("teamHandoverSchema_AnotherPersonWithoutAChoice_AsksForTheNewManager", () => {
  assert.deepEqual(reported({ ...teamHandoverValues(personId, version), reportsTo: "person" }), [
    `managerId: ${newManagerMissing}`,
  ]);
  assert.deepEqual(
    reported({ ...teamHandoverValues(personId, version), reportsTo: "person", managerId: "Asha Rao" }),
    [`managerId: ${newManagerMissing}`],
  );
});

test("teamHandoverSchema_AnotherPersonChosen_NamesThemAsTheNewManager", () => {
  assert.deepEqual(
    teamHandoverSchema.parse({ ...teamHandoverValues(personId, version), reportsTo: "person", managerId }),
    { personId, version, managerId },
  );
});

test("teamHandoverSchema_NoManagerChosen_NamesNoManagerWhateverThePickerHeld", () => {
  assert.deepEqual(
    teamHandoverSchema.parse({ ...teamHandoverValues(personId, version), reportsTo: "nobody", managerId }),
    { personId, version, managerId: null },
  );
  assert.deepEqual(
    teamHandoverSchema.parse({ ...teamHandoverValues(personId, version), reportsTo: "nobody" }),
    { personId, version, managerId: null },
  );
});

test("teamHandoverSchema_ChoiceNoControlOffers_IsRefusedAsMissing", () => {
  assert.deepEqual(reported({ ...teamHandoverValues(personId, version), reportsTo: "everyone" }), [
    `reportsTo: ${reportsToMissing}`,
  ]);
});

test("teamHandoverSchema_PersonOrVersionThePageDidNotHold_AsksForAReload", () => {
  assert.deepEqual(reported({ personId: "Meera", version: "", reportsTo: "nobody", managerId: "" }), [
    `personId: ${formMessages.pageOutOfDate}`,
    `version: ${formMessages.pageOutOfDate}`,
  ]);
});

test("teamHandoverProblemMessages_TheNewManagerRefused_IsWordedOnTheForm", () => {
  const refusal = (code: string) =>
    problemToFormState(new ApiError({ status: 422, code, traceId: "00-4bf9" }), {
      messages: teamHandoverProblemMessages,
    });

  assert.deepEqual(refusal("user.team-manager-inactive"), {
    status: "failed",
    message: "That person's record is deactivated, so they cannot take the team; choose an active person.",
    code: "user.team-manager-inactive",
    reference: "00-4bf9",
  });
  assert.deepEqual(
    [
      "user.team-manager-not-found",
      "user.team-manager-same-person",
      "user.team-manager-in-team",
      "user.team-reporting-cycle",
    ].map((code) => {
      const state = refusal(code);
      return state.status === "failed" ? state.message : state.status;
    }),
    [
      "That person's record no longer exists; choose someone else.",
      "The team cannot report to the person whose team it is; choose someone else.",
      "That person is in the team they would take over; choose someone outside the team.",
      "That person reports, through their own managers, to someone in the team, who would then report to themself; choose someone else.",
    ],
  );
});

test("teamHandoverProblemMessages_AdministratorWhoseOwnRecordWentMeanwhile_IsToldNothingChanged", () => {
  const state = problemToFormState(new ApiError({ status: 409, code: "user.actor-inactive" }), {
    messages: teamHandoverProblemMessages,
  });

  assert.equal(state.status === "failed" ? state.message : state.status, ownRecordInactive);
});
