"use server";

import type { RegisterPersonRequest } from "@dewiride/erp-api-client";
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

import { orderedRoles } from "../../roles/components/role-selection";
import { registerPersonProblemMessages, registerPersonSchema } from "../forms/register-person.schema";

const namingRolesForbidden =
  "You may register people but not give them roles. Clear the roles, or ask an administrator who may assign them.";

export async function registerPerson(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.register")) return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(registerPersonSchema, submission, { idempotent: true });
  if (!parsed.ok) return parsed.state;
  if (parsed.data.roles.length > 0 && !holdsPermission(person, "identity.users.people.assign-roles")) {
    return formFailed(namingRolesForbidden);
  }

  let registeredId: string | undefined;
  try {
    const registered = await callApi((client) =>
      client.api.identity.users.post(registration(parsed.data), {
        headers: { "Idempotency-Key": parsed.idempotencyKey },
      }),
    );
    registeredId = registered.id ?? undefined;
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: registerPersonProblemMessages });
  }
  if (registeredId === undefined) return formFailed(formMessages.serverFailed);
  redirect(`/identity/users/${registeredId}`, RedirectType.replace);
}

function registration(values: z.output<typeof registerPersonSchema>): RegisterPersonRequest {
  return {
    displayName: values.displayName,
    workEmail: values.workEmail,
    employeeCode: values.employeeCode ?? null,
    phoneNumber: values.phoneNumber ?? null,
    dateOfJoining: values.dateOfJoining === undefined ? null : apiDateOf(values.dateOfJoining),
    roles: [...orderedRoles(values.roles)],
  };
}
