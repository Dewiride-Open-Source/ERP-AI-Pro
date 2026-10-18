"use server";

import type { InvitePersonRequest, PersonResponse } from "@dewiride/erp-api-client";
import { unstable_rethrow } from "next/navigation";
import { z } from "zod";

import { callApi } from "@/shared/api/client";
import { apiDateOf } from "@/shared/api/dates/date-only";
import { ApiError } from "@/shared/api/problem-details";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";
import { parseSubmission } from "@/shared/forms/schemas/parse-submission";
import {
  formFailed,
  type FailedFormState,
  type FormState,
  type InvalidFormState,
} from "@/shared/forms/state/form-state";

import { pageOutOfDate, refusedOutcome } from "../../details/components/person-action-text";
import type { RegistrationAnswer } from "../../registration/server/actions";
import { orderedRoles } from "../../roles/components/role-selection";
import {
  directoryFailure,
  directoryMatches,
  readSearchText,
  searchTooShort,
  type DirectorySearchAnswer,
} from "../components/directory-search";
import { invitePersonProblemMessages, invitePersonSchema } from "../forms/invite-person.schema";

const searchText = z.string();

const invitingWithRolesForbidden =
  "You may invite people but not give them roles. Clear the roles, or ask an administrator who may assign them.";

export async function searchDirectory(text: string): Promise<DirectorySearchAnswer> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.register")) return { error: formMessages.forbidden };
  const parsed = searchText.safeParse(text);
  if (!parsed.success) return { error: pageOutOfDate };
  const reading = readSearchText(parsed.data);
  if (reading.status !== "ready") {
    return { error: reading.status === "refused" ? reading.message : searchTooShort };
  }

  try {
    const found = await callApi((client) =>
      client.api.identity.users.directory.get({ queryParameters: { search: reading.text } }),
    );
    return { people: directoryMatches(found.people ?? []), hasMore: found.hasMore ?? false };
  } catch (error) {
    unstable_rethrow(error);
    const failed = directoryRefusal(error) ?? refusedOutcome(problemToFormState(error));
    return { error: failed.message, reference: failed.reference };
  }
}

export async function invitePerson(_previous: FormState, submission: unknown): Promise<RegistrationAnswer> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.register")) return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(invitePersonSchema, submission, { idempotent: true });
  if (!parsed.ok) return parsed.state;
  if (parsed.data.roles.length > 0 && !holdsPermission(person, "identity.users.people.assign-roles")) {
    return formFailed(invitingWithRolesForbidden);
  }

  let invited: PersonResponse;
  try {
    invited = await callApi((client) =>
      client.api.identity.users.invitations.post(invitation(parsed.data), {
        headers: { "Idempotency-Key": parsed.idempotencyKey },
      }),
    );
  } catch (error) {
    unstable_rethrow(error);
    return invitationRefusal(error);
  }
  if (!invited.id || !invited.displayName) return formFailed(formMessages.serverFailed);
  return { status: "succeeded", personId: invited.id, personName: invited.displayName };
}

function directoryRefusal(error: unknown): FailedFormState | undefined {
  return error instanceof ApiError ? directoryFailure(error) : undefined;
}

function invitationRefusal(error: unknown): InvalidFormState | FailedFormState {
  return directoryRefusal(error) ?? problemToFormState(error, { messages: invitePersonProblemMessages });
}

function invitation(values: z.output<typeof invitePersonSchema>): InvitePersonRequest {
  return {
    entraObjectId: values.entraObjectId,
    employeeCode: values.employeeCode ?? null,
    phoneNumber: values.phoneNumber ?? null,
    dateOfJoining: values.dateOfJoining === undefined ? null : apiDateOf(values.dateOfJoining),
    roles: [...orderedRoles(values.roles)],
  };
}
