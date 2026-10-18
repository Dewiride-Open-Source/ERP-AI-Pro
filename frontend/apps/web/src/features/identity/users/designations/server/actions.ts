"use server";

import type { CreateDesignationRequest, UpdateDesignationRequest } from "@dewiride/erp-api-client";
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
  deleteDesignationProblemMessages,
  designationAlreadyDeleted,
  designationAlreadyDeletedCode,
} from "../components/designation-text";
import {
  addDesignationProblemMessages,
  addDesignationSchema,
  renameDesignationProblemMessages,
  renameDesignationSchema,
} from "../forms/designation.schema";

const designationId = z.uuid();

export async function createDesignation(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.designations.create"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(addDesignationSchema, submission, { idempotent: true });
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.identity.users.designations.post(creation(parsed.data), {
        headers: { "Idempotency-Key": parsed.idempotencyKey },
      }),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: addDesignationProblemMessages });
  }
  refresh();
  return formSucceeded();
}

export async function renameDesignation(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.designations.update"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(renameDesignationSchema, submission);
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.identity.users.designations.byId(parsed.data.id).put(rename(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: renameDesignationProblemMessages });
  }
  refresh();
  return formSucceeded();
}

export async function deleteDesignation(id: string): Promise<PersonActionOutcome> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.designations.delete"))
    return { done: false, message: formMessages.forbidden };
  const parsed = designationId.safeParse(id);
  if (!parsed.success) return { done: false, message: pageOutOfDate };

  let outcome: PersonActionOutcome = { done: true };
  try {
    await sendApi((client) => client.api.identity.users.designations.byId(parsed.data).delete());
  } catch (error) {
    unstable_rethrow(error);
    if (!(error instanceof ApiError && error.problem.code === designationAlreadyDeletedCode)) {
      return refusedOutcome(problemToFormState(error, { messages: deleteDesignationProblemMessages }));
    }
    outcome = { done: true, message: designationAlreadyDeleted };
  }
  refresh();
  return outcome;
}

function creation(values: z.output<typeof addDesignationSchema>): CreateDesignationRequest {
  return { name: values.name };
}

function rename(values: z.output<typeof renameDesignationSchema>): UpdateDesignationRequest {
  return { name: values.name, version: values.version };
}
