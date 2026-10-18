import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import { personGone, recordVersionSchema } from "../../registration/forms/register-person.schema.ts";
import { ownRecordInactive, ownStatusRefused } from "../components/person-action-text.ts";

export const reportsToChoices = ["person", "nobody"] as const;

export const reportsToMissing = "Choose who the team reports to from today.";

export const newManagerMissing = "Choose their new manager from the list.";

const chosenManager = z.uuid();

export const teamHandoverSchema = z
  .object({
    personId: z.uuid({ error: formMessages.pageOutOfDate }),
    version: recordVersionSchema,
    reportsTo: z
      .string({ error: reportsToMissing })
      .pipe(z.enum(reportsToChoices, { error: reportsToMissing })),
    managerId: z.string({ error: newManagerMissing }),
  })
  .superRefine(({ reportsTo, managerId }, context) => {
    if (reportsTo === "person" && !chosenManager.safeParse(managerId).success) {
      context.addIssue({ code: "custom", path: ["managerId"], message: newManagerMissing });
    }
  })
  .transform(({ personId, version, reportsTo, managerId }) => ({
    personId,
    version,
    managerId: reportsTo === "person" ? managerId : null,
  }));

export type TeamHandoverValues = z.input<typeof teamHandoverSchema>;

export type TeamHandoverChoice = z.output<typeof teamHandoverSchema>;

export function teamHandoverValues(personId: string, version: string): TeamHandoverValues {
  return { personId, version, reportsTo: "", managerId: "" };
}

export const teamHandoverProblemMessages: ProblemMessages = {
  "user.team-manager-not-found": "That person's record no longer exists; choose someone else.",
  "user.team-manager-inactive":
    "That person's record is deactivated, so they cannot take the team; choose an active person.",
  "user.team-manager-same-person":
    "The team cannot report to the person whose team it is; choose someone else.",
  "user.team-manager-in-team":
    "That person is in the team they would take over; choose someone outside the team.",
  "user.team-reporting-cycle":
    "That person reports, through their own managers, to someone in the team, who would then report to themself; choose someone else.",
  "user.actor-inactive": ownRecordInactive,
  "user.own-status": ownStatusRefused,
  "user.not-found": personGone,
  "concurrency.conflict":
    "Someone else changed this record or the organisation meanwhile. Reload the page, then try again.",
};
