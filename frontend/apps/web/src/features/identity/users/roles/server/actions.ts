"use server";

import type { AssignPersonRolesRequest } from "@dewiride/erp-api-client";
import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import type { z } from "zod";

import { callApi } from "@/shared/api/client";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";
import { parseSubmission } from "@/shared/forms/schemas/parse-submission";
import { formFailed, formSucceeded, type FormState } from "@/shared/forms/state/form-state";

import { orderedRoles } from "../components/role-selection";
import { assignRolesProblemMessages, assignRolesSchema } from "../forms/assign-roles.schema";

export async function assignPersonRoles(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.assign-roles"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(assignRolesSchema, submission);
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.identity.users.byId(parsed.data.personId).roles.put(assignment(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: assignRolesProblemMessages });
  }
  refresh();
  return formSucceeded();
}

function assignment(values: z.output<typeof assignRolesSchema>): AssignPersonRolesRequest {
  return { roles: [...orderedRoles(values.roles)], version: values.version };
}
