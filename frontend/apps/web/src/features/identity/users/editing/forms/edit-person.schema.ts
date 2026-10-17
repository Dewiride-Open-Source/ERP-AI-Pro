import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import {
  dateOfJoiningSchema,
  employeeCodeSchema,
  employeeCodeTaken,
  personGone,
  phoneNumberSchema,
  recordVersionSchema,
} from "../../registration/forms/register-person.schema.ts";

export const editPersonSchema = z.object({
  id: z.uuid({ error: formMessages.pageOutOfDate }),
  employeeCode: employeeCodeSchema,
  phoneNumber: phoneNumberSchema,
  dateOfJoining: dateOfJoiningSchema,
  version: recordVersionSchema,
});

export type EditPersonValues = z.input<typeof editPersonSchema>;

export const editPersonProblemMessages: ProblemMessages = {
  "user.employee-code-taken": employeeCodeTaken,
  "user.not-found": personGone,
  "concurrency.conflict":
    "Someone else changed this record after the page was loaded. Reload the page, then make your changes again.",
};
