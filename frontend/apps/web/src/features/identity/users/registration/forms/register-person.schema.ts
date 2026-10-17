import type { PersonRole } from "@dewiride/erp-api-client";
import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import { calendarDateSchema, optionalInput, requiredText } from "@/shared/forms/schemas/field-schemas";

export const displayNameMaxLength = 256;

export const workEmailMaxLength = 254;

export const employeeCodeMaxLength = 32;

export const phoneNumberMaxLength = 32;

export const recordVersionMaxLength = 64;

export const assignableRoles = ["finance", "hr", "manager"] as const satisfies readonly PersonRole[];

export const employeeCodeMalformed =
  "Use letters, digits, hyphens, slashes and underscores, starting with a letter or a digit.";

export const phoneNumberMalformed =
  "Enter a phone number with + and the country code, such as +44 20 7946 0958, or a 10-digit Indian mobile number.";

export const employeeCodeTaken = "Another person already has this employee code.";

export const personGone = "This person's record no longer exists.";

const nameMissing = "Enter the person's name.";

const workEmailMissing = "Enter their work email.";

const workEmailMalformed = "Enter a work email such as name@example.com.";

const roleUnknown = "Choose roles from the list.";

const employeeCodePattern = /^[A-Za-z0-9][A-Za-z0-9/_-]*$/;

const phoneNumberCharacters = /^\+?[0-9 ()-]*$/;

const indianCountryCode = "91";

// The API's rule for a phone number, applied before anything is sent: an international number is + and its country code
// and has 8 to 15 digits (ITU-T Recommendation E.164); one without them is an Indian mobile number of ten digits starting
// with 6, 7, 8 or 9, with or without a leading 0, as the national numbering plan of India writes them.
export function isPhoneNumber(text: string): boolean {
  const value = text.trim();
  if (!phoneNumberCharacters.test(value)) return false;
  let digits = value.replace(/[^0-9]/g, "");
  if (!value.startsWith("+")) {
    if (digits.length === 11 && digits.startsWith("0")) digits = digits.slice(1);
    if (digits.length !== 10 || !/^[6-9]/.test(digits)) return false;
    digits = `${indianCountryCode}${digits}`;
  }
  return digits.length >= 8 && digits.length <= 15 && !digits.startsWith("0");
}

export const employeeCodeSchema = optionalInput(
  z
    .string()
    .trim()
    .max(employeeCodeMaxLength, { error: `Use ${employeeCodeMaxLength} characters or fewer.`, abort: true })
    .regex(employeeCodePattern, { error: employeeCodeMalformed }),
);

export const phoneNumberSchema = optionalInput(
  z
    .string()
    .trim()
    .max(phoneNumberMaxLength, { error: `Use ${phoneNumberMaxLength} characters or fewer.`, abort: true })
    .refine(isPhoneNumber, { error: phoneNumberMalformed }),
);

export const dateOfJoiningSchema = calendarDateSchema({ required: false });

export const rolesSchema = z
  .array(z.enum(assignableRoles, { error: roleUnknown }), { error: roleUnknown })
  .max(assignableRoles.length, { error: roleUnknown });

export const recordVersionSchema = z
  .string({ error: formMessages.pageOutOfDate })
  .min(1, { error: formMessages.pageOutOfDate })
  .max(recordVersionMaxLength, { error: formMessages.pageOutOfDate });

export const registerPersonSchema = z.object({
  displayName: requiredText(displayNameMaxLength, { required: nameMissing }),
  workEmail: requiredText(workEmailMaxLength, { required: workEmailMissing })
    .toLowerCase()
    .pipe(z.email({ error: workEmailMalformed })),
  employeeCode: employeeCodeSchema,
  phoneNumber: phoneNumberSchema,
  dateOfJoining: dateOfJoiningSchema,
  roles: rolesSchema,
});

export type RegisterPersonValues = z.input<typeof registerPersonSchema>;

export const defaultRegisterPersonValues: RegisterPersonValues = {
  displayName: "",
  workEmail: "",
  employeeCode: "",
  phoneNumber: "",
  dateOfJoining: "",
  roles: [],
};

export const registerPersonProblemMessages: ProblemMessages = {
  "user.work-email-taken": "A person with this work email is already registered.",
  "user.employee-code-taken": employeeCodeTaken,
  "concurrency.conflict":
    "Someone registered a person with this work email or employee code at the same moment. Check People, then try again.",
};
