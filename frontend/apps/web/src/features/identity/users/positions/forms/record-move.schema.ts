import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import { calendarDateSchema, optionalInput } from "@/shared/forms/schemas/field-schemas";

import { personGone } from "../../registration/forms/register-person.schema.ts";

export const choiceGone = "That choice no longer exists; choose again.";

export const organisationBusy = "Another organisation change is being saved; try again.";

export const recordMoveSchema = z.object({
  personId: z.uuid({ error: formMessages.pageOutOfDate }),
  effectiveFrom: calendarDateSchema({
    required: true,
    requiredMessage: "Enter the date the move takes effect.",
  }),
  departmentId: optionalInput(z.uuid({ error: "Choose a department from the list." })),
  designationId: optionalInput(z.uuid({ error: "Choose a designation from the list." })),
  managerId: optionalInput(z.uuid({ error: "Choose a manager from the list." })),
});

export type RecordMoveValues = z.input<typeof recordMoveSchema>;

export function recordMoveValues(personId: string): RecordMoveValues {
  return { personId, effectiveFrom: "", departmentId: "", designationId: "", managerId: "" };
}

export const recordMoveProblemMessages: ProblemMessages = {
  "position.date-taken": "A move from this date already exists; withdraw it first.",
  "position.department-not-found": choiceGone,
  "position.designation-not-found": choiceGone,
  "position.manager-not-found": choiceGone,
  "position.manager-inactive":
    "That manager's record is deactivated, so they cannot manage anyone today or later; choose an active manager.",
  "position.self-managed": "A person cannot be their own manager; choose someone else.",
  "position.reporting-cycle":
    "This move would make the person report, through their managers, to themself; choose another manager.",
  "concurrency.conflict": organisationBusy,
  "user.not-found": personGone,
};
