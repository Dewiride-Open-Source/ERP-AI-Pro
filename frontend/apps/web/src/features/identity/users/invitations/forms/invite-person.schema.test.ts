import assert from "node:assert/strict";
import { test } from "node:test";

import { ApiError } from "@/shared/api/problem-details";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";

import {
  employeeCodeMalformed,
  employeeCodeMaxLength,
  phoneNumberMalformed,
} from "../../registration/forms/register-person.schema.ts";

import {
  invitePersonProblemMessages,
  invitePersonSchema,
  invitePersonValues,
  personNotChosen,
  type InvitePersonValues,
} from "./invite-person.schema.ts";

const objectId = "4a1d6d4e-5c3f-4b0e-9a41-0d6f0c2b7e11";

const complete: InvitePersonValues = {
  entraObjectId: objectId,
  employeeCode: " DW-0042 ",
  phoneNumber: "98765 43210",
  dateOfJoining: "2026-04-01",
  roles: ["finance", "manager"],
};

function reported(values: unknown): string[] {
  const result = invitePersonSchema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("invitePersonSchema_ValuesOfAChosenPerson_AskForNothingMore", () => {
  assert.deepEqual(invitePersonSchema.parse(invitePersonValues(objectId)), {
    entraObjectId: objectId,
    employeeCode: undefined,
    phoneNumber: undefined,
    dateOfJoining: undefined,
    roles: [],
  });
});

test("invitePersonSchema_CompleteValues_ParsesTheTrimmedDetails", () => {
  assert.deepEqual(invitePersonSchema.parse(complete), {
    entraObjectId: objectId,
    employeeCode: "DW-0042",
    phoneNumber: "98765 43210",
    dateOfJoining: "2026-04-01",
    roles: ["finance", "manager"],
  });
});

test("invitePersonSchema_ObjectIdOfAnyGuidForm_IsAccepted", () => {
  for (const id of ["00000003-0000-0000-c000-000000000000", "019A3C5E-7D1F-7A2B-9C3D-4E5F6A7B8C9D"]) {
    assert.deepEqual(reported({ ...complete, entraObjectId: id }), [], id);
  }
});

test("invitePersonSchema_NoPersonChosen_AsksToChooseOneFromTheDirectory", () => {
  for (const entraObjectId of ["", "not-a-guid", "00000000-0000-0000-0000-000000000000", undefined]) {
    assert.deepEqual(
      reported({ ...complete, entraObjectId }),
      [`entraObjectId: ${personNotChosen}`],
      String(entraObjectId),
    );
  }
});

const refusals: readonly {
  readonly change: Partial<Record<keyof InvitePersonValues, unknown>>;
  readonly expected: string;
}[] = [
  {
    change: { employeeCode: "D".repeat(employeeCodeMaxLength + 1) },
    expected: `employeeCode: Use ${employeeCodeMaxLength} characters or fewer.`,
  },
  { change: { employeeCode: "-0042" }, expected: `employeeCode: ${employeeCodeMalformed}` },
  { change: { phoneNumber: "12345" }, expected: `phoneNumber: ${phoneNumberMalformed}` },
  {
    change: { dateOfJoining: "31-02-2026" },
    expected: "dateOfJoining: Enter a real date as day-month-year, for example 31-03-2026.",
  },
  { change: { roles: ["administrator"] }, expected: "roles.0: Choose roles from the list." },
];

test("invitePersonSchema_OneFieldRefused_ReportsOnlyThatField", () => {
  for (const { change, expected } of refusals) {
    assert.deepEqual(reported({ ...complete, ...change }), [expected], JSON.stringify(change));
  }
});

test("invitePersonProblemMessages_RefusalsOfTheApi_AreWordedForThePerson", () => {
  assert.deepEqual(invitePersonProblemMessages, {
    "directory.person-not-found":
      "This person is no longer in the company directory. Go back to the search and choose again.",
    "user.entra-object-id-taken":
      "This person is already in the ERP. Go back to the search to open their page.",
    "user.work-email-taken":
      "Someone registered this person by their work email meanwhile. Go back to the search to open their page.",
    "user.employee-code-taken": "Another person already has this employee code.",
    "concurrency.conflict":
      "Someone invited this person or used this employee code at the same moment. Go back to the search, then try again.",
  });
});

test("invitePersonProblemMessages_PersonAlreadyInTheErp_ShowsThePlainSentenceAndKeepsTheCode", () => {
  const taken = new ApiError({
    status: 409,
    code: "user.entra-object-id-taken",
    detail: "Another person's record already has this Entra object id.",
    traceId: "0af7651916cd43dd8448eb211c80319c",
  });

  assert.deepEqual(problemToFormState(taken, { messages: invitePersonProblemMessages }), {
    status: "failed",
    message: "This person is already in the ERP. Go back to the search to open their page.",
    code: "user.entra-object-id-taken",
    reference: "0af7651916cd43dd8448eb211c80319c",
  });
});
