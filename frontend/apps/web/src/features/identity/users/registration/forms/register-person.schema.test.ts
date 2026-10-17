import assert from "node:assert/strict";
import { test } from "node:test";

import {
  defaultRegisterPersonValues,
  displayNameMaxLength,
  employeeCodeMalformed,
  employeeCodeMaxLength,
  isPhoneNumber,
  phoneNumberMalformed,
  phoneNumberMaxLength,
  recordVersionSchema,
  registerPersonSchema,
  type RegisterPersonValues,
} from "./register-person.schema.ts";

const complete: RegisterPersonValues = {
  displayName: "  Meera Nair ",
  workEmail: " Meera.Nair@Example.com ",
  employeeCode: " DW-0042 ",
  phoneNumber: "98765 43210",
  dateOfJoining: "2026-04-01",
  roles: ["finance", "manager"],
};

function reported(values: unknown): string[] {
  const result = registerPersonSchema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("registerPersonSchema_DefaultValues_AsksForTheNameAndTheWorkEmailOnly", () => {
  assert.deepEqual(reported(defaultRegisterPersonValues), [
    "displayName: Enter the person's name.",
    "workEmail: Enter their work email.",
  ]);
});

test("registerPersonSchema_CompleteValues_ParsesTheTrimmedPersonWithALowerCaseEmail", () => {
  assert.deepEqual(registerPersonSchema.parse(complete), {
    displayName: "Meera Nair",
    workEmail: "meera.nair@example.com",
    employeeCode: "DW-0042",
    phoneNumber: "98765 43210",
    dateOfJoining: "2026-04-01",
    roles: ["finance", "manager"],
  });
});

test("registerPersonSchema_OptionalFieldsLeftEmpty_AreAbsent", () => {
  const parsed = registerPersonSchema.parse({
    ...complete,
    employeeCode: " ",
    phoneNumber: "",
    dateOfJoining: "",
    roles: [],
  });

  assert.equal(parsed.employeeCode, undefined);
  assert.equal(parsed.phoneNumber, undefined);
  assert.equal(parsed.dateOfJoining, undefined);
  assert.deepEqual(parsed.roles, []);
});

const refusals: readonly {
  readonly change: Partial<Record<keyof RegisterPersonValues, unknown>>;
  readonly expected: string;
}[] = [
  {
    change: { displayName: "M".repeat(displayNameMaxLength + 1) },
    expected: `displayName: Use ${displayNameMaxLength} characters or fewer.`,
  },
  {
    change: { workEmail: "meera.nair" },
    expected: "workEmail: Enter a work email such as name@example.com.",
  },
  {
    change: { employeeCode: "D".repeat(employeeCodeMaxLength + 1) },
    expected: `employeeCode: Use ${employeeCodeMaxLength} characters or fewer.`,
  },
  { change: { employeeCode: "-0042" }, expected: `employeeCode: ${employeeCodeMalformed}` },
  { change: { employeeCode: "DW 0042" }, expected: `employeeCode: ${employeeCodeMalformed}` },
  { change: { phoneNumber: "12345" }, expected: `phoneNumber: ${phoneNumberMalformed}` },
  {
    change: { phoneNumber: "9".repeat(phoneNumberMaxLength + 1) },
    expected: `phoneNumber: Use ${phoneNumberMaxLength} characters or fewer.`,
  },
  {
    change: { dateOfJoining: "31-02-2026" },
    expected: "dateOfJoining: Enter a real date as day-month-year, for example 31-03-2026.",
  },
  { change: { roles: ["administrator"] }, expected: "roles.0: Choose roles from the list." },
];

test("registerPersonSchema_OneFieldRefused_ReportsOnlyThatField", () => {
  for (const { change, expected } of refusals) {
    assert.deepEqual(reported({ ...complete, ...change }), [expected], JSON.stringify(change));
  }
});

test("isPhoneNumber_NumbersTheApiAccepts_AreAccepted", () => {
  for (const number of [
    "9876543210",
    "09876543210",
    "98765 43210",
    "(98765) 43-210",
    "+91 98765 43210",
    "+44 20 7946 0958",
    "+1 (415) 555-0100",
    "+12345678",
    "+123456789012345",
  ]) {
    assert.equal(isPhoneNumber(number), true, number);
  }
});

test("isPhoneNumber_NumbersTheApiRefuses_AreRefused", () => {
  for (const number of [
    "5876543210",
    "987654321",
    "98765432101",
    "9876543210 ext 2",
    "98765+43210",
    "++919876543210",
    "+91+9876543210",
    "+1234567",
    "+1234567890123456",
    "+0123456789",
    "",
  ]) {
    assert.equal(isPhoneNumber(number), false, number);
  }
});

test("recordVersionSchema_MissingOrOverlongVersion_AsksForAReload", () => {
  for (const version of ["", "A".repeat(65), undefined]) {
    const result = recordVersionSchema.safeParse(version);
    assert.equal(result.success, false, String(version));
    assert.equal(
      result.error?.issues[0]?.message,
      "This page is out of date. Reload the page, then send the form again.",
    );
  }
  assert.equal(recordVersionSchema.parse("AAAAAAAAB9E="), "AAAAAAAAB9E=");
});
