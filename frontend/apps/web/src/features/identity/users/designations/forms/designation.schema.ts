import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import { requiredText } from "@/shared/forms/schemas/field-schemas";

import { recordVersionSchema } from "../../registration/forms/register-person.schema.ts";

export const designationNameMaxLength = 100;

export const designationNameTaken = "Another designation already has this name; choose another name.";

const designationName = requiredText(designationNameMaxLength, { required: "Enter the designation's name." });

export const addDesignationSchema = z.object({
  name: designationName,
});

export type AddDesignationValues = z.input<typeof addDesignationSchema>;

export const defaultAddDesignationValues: AddDesignationValues = { name: "" };

export const renameDesignationSchema = z.object({
  id: z.uuid({ error: formMessages.pageOutOfDate }),
  name: designationName,
  version: recordVersionSchema,
});

export type RenameDesignationValues = z.input<typeof renameDesignationSchema>;

export function renameDesignationValues(designation: {
  readonly id: string;
  readonly name: string;
  readonly version: string;
}): RenameDesignationValues {
  return { id: designation.id, name: designation.name, version: designation.version };
}

export const addDesignationProblemMessages: ProblemMessages = {
  "designation.name-taken": designationNameTaken,
  "concurrency.conflict":
    "Someone added a designation with this name at the same moment. Check the list, then try again.",
};

export const renameDesignationProblemMessages: ProblemMessages = {
  "designation.name-taken": designationNameTaken,
  "designation.not-found":
    "This designation no longer exists. Reload the page to see the designations as they are now.",
  "concurrency.conflict":
    "Someone changed this designation after the page was loaded, or gave another one this name at the same moment. Reload the page, then try again.",
};
