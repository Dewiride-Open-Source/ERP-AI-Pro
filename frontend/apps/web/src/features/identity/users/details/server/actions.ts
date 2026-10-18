"use server";

import type { ChangePersonStatusRequest } from "@dewiride/erp-api-client";
import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import { z } from "zod";

import { callApi, sendApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";

import { recordVersionSchema } from "../../registration/forms/register-person.schema";
import {
  deletionProblemMessages,
  pageOutOfDate,
  personAlreadyDeleted,
  refusedOutcome,
  statusProblemMessages,
  type PersonActionOutcome,
} from "../components/person-action-text";

const personId = z.uuid();

const statusChangeRequest = z.object({
  id: personId,
  status: z.enum(["active", "deactivated"]),
  version: recordVersionSchema,
});

const personNotFound = "user.not-found";

export async function changePersonStatus(
  id: string,
  status: string,
  version: string,
): Promise<PersonActionOutcome> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.change-status"))
    return { done: false, message: formMessages.forbidden };
  const parsed = statusChangeRequest.safeParse({ id, status, version });
  if (!parsed.success) return { done: false, message: pageOutOfDate };

  try {
    await callApi((client) =>
      client.api.identity.users.byId(parsed.data.id).status.put(statusRequest(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return refusedOutcome(problemToFormState(error, { messages: statusProblemMessages }));
  }
  refresh();
  return { done: true };
}

export async function deletePerson(id: string): Promise<PersonActionOutcome> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.delete"))
    return { done: false, message: formMessages.forbidden };
  const parsed = personId.safeParse(id);
  if (!parsed.success) return { done: false, message: pageOutOfDate };

  try {
    await sendApi((client) => client.api.identity.users.byId(parsed.data).delete());
  } catch (error) {
    unstable_rethrow(error);
    if (error instanceof ApiError && error.problem.code === personNotFound) {
      return { done: true, message: personAlreadyDeleted };
    }
    return refusedOutcome(problemToFormState(error, { messages: deletionProblemMessages }));
  }
  return { done: true };
}

function statusRequest({ status, version }: z.output<typeof statusChangeRequest>): ChangePersonStatusRequest {
  return { status, version };
}
