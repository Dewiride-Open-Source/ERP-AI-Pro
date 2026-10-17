import type { PersonRole } from "@dewiride/erp-api-client";
import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import {
  personGone,
  recordVersionSchema,
  rolesSchema,
} from "../../registration/forms/register-person.schema.ts";

export const assignRolesSchema = z.object({
  personId: z.uuid({ error: formMessages.pageOutOfDate }),
  roles: rolesSchema,
  version: recordVersionSchema,
});

export type AssignRolesValues = z.input<typeof assignRolesSchema>;

export function assignRolesValues(
  personId: string,
  roles: readonly PersonRole[],
  version: string,
): AssignRolesValues {
  return { personId, roles: [...roles], version };
}

export const assignRolesProblemMessages: ProblemMessages = {
  "concurrency.conflict":
    "Someone else changed this record after the page was loaded. Reload the page, then assign the roles again.",
  "user.not-found": personGone,
};
