import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import { optionalInput, requiredText } from "@/shared/forms/schemas/field-schemas";

import { organisationBusy } from "../../positions/forms/record-move.schema.ts";
import { recordVersionSchema } from "../../registration/forms/register-person.schema.ts";

export const departmentNameMaxLength = 100;

export const departmentNameTaken =
  "Another department in the same place already has this name; choose another name or another place.";

export const departmentParentGone =
  "The department you chose to put it under no longer exists; choose again.";

const departmentName = requiredText(departmentNameMaxLength, { required: "Enter the department's name." });

const parentDepartment = optionalInput(z.uuid({ error: "Choose a department from the list." }));

export const addDepartmentSchema = z.object({
  name: departmentName,
  parentId: parentDepartment,
});

export type AddDepartmentValues = z.input<typeof addDepartmentSchema>;

export function addDepartmentValues(parentId: string | undefined): AddDepartmentValues {
  return { name: "", parentId: parentId ?? "" };
}

export const editDepartmentSchema = z.object({
  id: z.uuid({ error: formMessages.pageOutOfDate }),
  name: departmentName,
  parentId: parentDepartment,
  version: recordVersionSchema,
});

export type EditDepartmentValues = z.input<typeof editDepartmentSchema>;

export function editDepartmentValues(department: {
  readonly id: string;
  readonly name: string;
  readonly parentId: string | undefined;
  readonly version: string;
}): EditDepartmentValues {
  return {
    id: department.id,
    name: department.name,
    parentId: department.parentId ?? "",
    version: department.version,
  };
}

export const addDepartmentProblemMessages: ProblemMessages = {
  "department.name-taken": departmentNameTaken,
  "department.parent-not-found": departmentParentGone,
  "concurrency.conflict": organisationBusy,
};

export const editDepartmentProblemMessages: ProblemMessages = {
  "department.name-taken": departmentNameTaken,
  "department.parent-not-found": departmentParentGone,
  "department.would-contain-itself":
    "A department cannot go under itself or under a department within it; choose another place.",
  "department.not-found":
    "This department no longer exists. Reload the page to see the departments as they are now.",
  "concurrency.conflict":
    "Someone changed this department after the page was loaded, or another change to the organisation is being saved. Reload the page, then try again.",
};
