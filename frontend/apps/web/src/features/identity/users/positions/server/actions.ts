"use server";

import type { RecordPositionRequest } from "@dewiride/erp-api-client";
import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import { z } from "zod";

import { callApi, sendApi } from "@/shared/api/client";
import { apiDateOf } from "@/shared/api/dates/date-only";
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
import type { PersonSearch } from "../../organisation/hooks/use-person-search";
import { searchActivePeople } from "../../organisation/server/queries";
import { displayNameMaxLength } from "../../registration/forms/register-person.schema";
import {
  moveAlreadyWithdrawn,
  moveAlreadyWithdrawnCode,
  withdrawMoveProblemMessages,
} from "../components/move-text";
import { recordMoveProblemMessages, recordMoveSchema } from "../forms/record-move.schema";

const personId = z.uuid();

const moveOfPerson = z.object({ id: personId, positionId: z.uuid() });

const managerQuery = z.object({ id: personId, text: z.string() });

export async function recordPersonMove(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.positions.record")) return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(recordMoveSchema, submission, { idempotent: true });
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.identity.users.byId(parsed.data.personId).positions.post(move(parsed.data), {
        headers: { "Idempotency-Key": parsed.idempotencyKey },
      }),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: recordMoveProblemMessages });
  }
  refresh();
  return formSucceeded();
}

export async function withdrawPersonMove(id: string, positionId: string): Promise<PersonActionOutcome> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.positions.withdraw"))
    return { done: false, message: formMessages.forbidden };
  const parsed = moveOfPerson.safeParse({ id, positionId });
  if (!parsed.success) return { done: false, message: pageOutOfDate };

  let outcome: PersonActionOutcome = { done: true };
  try {
    await sendApi((client) =>
      client.api.identity.users.byId(parsed.data.id).positions.byPositionId(parsed.data.positionId).delete(),
    );
  } catch (error) {
    unstable_rethrow(error);
    if (!(error instanceof ApiError && error.problem.code === moveAlreadyWithdrawnCode)) {
      return refusedOutcome(problemToFormState(error, { messages: withdrawMoveProblemMessages }));
    }
    outcome = { done: true, message: moveAlreadyWithdrawn };
  }
  refresh();
  return outcome;
}

export async function searchManagers(id: string, text: string): Promise<PersonSearch> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.read")) return { error: formMessages.forbidden };
  const parsed = managerQuery.safeParse({ id, text });
  if (!parsed.success) return { error: pageOutOfDate };
  if (parsed.data.text.trim().length > displayNameMaxLength) return { options: [] };

  try {
    return { options: await searchActivePeople(parsed.data.text, { excluding: parsed.data.id }) };
  } catch (error) {
    unstable_rethrow(error);
    return { error: refusedOutcome(problemToFormState(error)).message };
  }
}

function move(values: z.output<typeof recordMoveSchema>): RecordPositionRequest {
  return {
    effectiveFrom: apiDateOf(values.effectiveFrom),
    departmentId: values.departmentId ?? null,
    designationId: values.designationId ?? null,
    managerId: values.managerId ?? null,
  };
}
