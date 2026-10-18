import { z } from "zod";

import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import {
  dateOfJoiningSchema,
  employeeCodeSchema,
  employeeCodeTaken,
  phoneNumberSchema,
  rolesSchema,
} from "../../registration/forms/register-person.schema.ts";

export const personNotChosen = "Choose a person from the company directory first.";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

// The object id comes from the directory search, never from typing, and an Entra object id is any GUID, as the API binds it;
// the empty GUID names nobody, so the API refuses it.
export const invitePersonSchema = z.object({
  entraObjectId: z
    .guid({ error: personNotChosen })
    .refine((id) => id !== emptyGuid, { error: personNotChosen }),
  employeeCode: employeeCodeSchema,
  phoneNumber: phoneNumberSchema,
  dateOfJoining: dateOfJoiningSchema,
  roles: rolesSchema,
});

export type InvitePersonValues = z.input<typeof invitePersonSchema>;

export function invitePersonValues(entraObjectId: string): InvitePersonValues {
  return { entraObjectId, employeeCode: "", phoneNumber: "", dateOfJoining: "", roles: [] };
}

export const invitePersonProblemMessages: ProblemMessages = {
  "directory.person-not-found":
    "This person is no longer in the company directory. Go back to the search and choose again.",
  "user.entra-object-id-taken":
    "This person is already in the ERP. Go back to the search to open their page.",
  "user.work-email-taken":
    "Someone registered this person by their work email meanwhile. Go back to the search to open their page.",
  "user.employee-code-taken": employeeCodeTaken,
  "concurrency.conflict":
    "Someone invited this person or used this employee code at the same moment. Go back to the search, then try again.",
};
