"use server";

import type { CreateDepartmentRequest, UpdateDepartmentRequest } from "@dewiride/erp-api-client";
import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import { z } from "zod";

import { callApi, sendApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";
import { parseSubmission } from "@/shared/forms/schemas/parse-submission";
import { formFailed, formSucceeded, type FormState } from "@/shared/forms/state/form-state";

import {
  pageOutOfDate,
  refusedOutcome,
  type PersonActionOutcome,
} from "../../details/components/person-action-text";
import {
  departmentAlreadyDeleted,
  departmentAlreadyDeletedCode,
  deleteDepartmentProblemMessages,
} from "../components/department-text";
import {
  addDepartmentProblemMessages,
  addDepartmentSchema,
  editDepartmentProblemMessages,
  editDepartmentSchema,
} from "../forms/department.schema";

const departmentId = z.uuid();

export async function createDepartment(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.departments.create"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(addDepartmentSchema, submission, { idempotent: true });
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.identity.users.departments.post(creation(parsed.data), {
        headers: { "Idempotency-Key": parsed.idempotencyKey },
      }),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: addDepartmentProblemMessages });
  }
  refresh();
  return formSucceeded();
}

export async function updateDepartment(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.departments.update"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(editDepartmentSchema, submission);
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.identity.users.departments.byId(parsed.data.id).put(update(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: editDepartmentProblemMessages });
  }
  refresh();
  return formSucceeded();
}

export async function deleteDepartment(id: string): Promise<PersonActionOutcome> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.departments.delete"))
    return { done: false, message: formMessages.forbidden };
  const parsed = departmentId.safeParse(id);
  if (!parsed.success) return { done: false, message: pageOutOfDate };

  let outcome: PersonActionOutcome = { done: true };
  try {
    await sendApi((client) => client.api.identity.users.departments.byId(parsed.data).delete());
  } catch (error) {
    unstable_rethrow(error);
    if (!(error instanceof ApiError && error.problem.code === departmentAlreadyDeletedCode)) {
      return refusedOutcome(problemToFormState(error, { messages: deleteDepartmentProblemMessages }));
    }
    outcome = { done: true, message: departmentAlreadyDeleted };
  }
  refresh();
  return outcome;
}

function creation(values: z.output<typeof addDepartmentSchema>): CreateDepartmentRequest {
  return { name: values.name, parentId: values.parentId ?? null };
}

function update(values: z.output<typeof editDepartmentSchema>): UpdateDepartmentRequest {
  return { name: values.name, parentId: values.parentId ?? null, version: values.version };
}
