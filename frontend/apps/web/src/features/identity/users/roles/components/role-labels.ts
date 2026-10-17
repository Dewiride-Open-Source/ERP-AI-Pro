import type { PersonRole } from "@dewiride/erp-api-client";

import { assignableRoles } from "../../registration/forms/register-person.schema.ts";

export const roleLabels: Readonly<Record<PersonRole, string>> = {
  finance: "Finance",
  hr: "HR",
  manager: "Manager",
};

export function orderedRoles(roles: readonly PersonRole[]): readonly PersonRole[] {
  return assignableRoles.filter((role) => roles.includes(role));
}

export function withRole(roles: readonly PersonRole[], role: PersonRole, held: boolean): PersonRole[] {
  return assignableRoles.filter((candidate) => (candidate === role ? held : roles.includes(candidate)));
}
