"use server";

import type { UpdatePersonRequest } from "@dewiride/erp-api-client";
import { redirect, RedirectType, unstable_rethrow } from "next/navigation";
import type { z } from "zod";

import { callApi } from "@/shared/api/client";
import { apiDateOf } from "@/shared/api/dates/date-only";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";
import { parseSubmission } from "@/shared/forms/schemas/parse-submission";
import { formFailed, type FormState } from "@/shared/forms/state/form-state";

import { editPersonProblemMessages, editPersonSchema } from "../forms/edit-person.schema";

export async function updatePerson(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.update")) return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(editPersonSchema, submission);
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) => client.api.identity.users.byId(parsed.data.id).put(update(parsed.data)));
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: editPersonProblemMessages });
  }
  redirect(`/identity/users/${parsed.data.id}`, RedirectType.replace);
}

function update(values: z.output<typeof editPersonSchema>): UpdatePersonRequest {
  return {
    employeeCode: values.employeeCode ?? null,
    phoneNumber: values.phoneNumber ?? null,
    dateOfJoining: values.dateOfJoining === undefined ? null : apiDateOf(values.dateOfJoining),
    version: values.version,
  };
}
