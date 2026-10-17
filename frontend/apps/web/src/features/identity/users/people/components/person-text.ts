import type { ErpRole, PersonRole, PersonStatus } from "@dewiride/erp-api-client";

import { formatDateTimeIst } from "@/shared/format/dates";

import { assignableRoles } from "../../registration/forms/register-person.schema.ts";

const statusLabels: Readonly<Record<PersonStatus, string>> = {
  active: "Active",
  deactivated: "Deactivated",
};

const roleLabels: Readonly<Record<ErpRole, string>> = {
  employee: "Employee",
  finance: "Finance",
  hr: "HR",
  manager: "Manager",
  administrator: "Administrator",
};

export const notRecorded = "—";

export const unknownDepartment = "Unknown department";

export const unknownDesignation = "Unknown designation";

export function statusLabel(status: PersonStatus | undefined): string {
  return status === undefined ? notRecorded : statusLabels[status];
}

export function roleLabel(role: ErpRole): string {
  return roleLabels[role];
}

// The roles are those assigned in the ERP; Administrator and Employee come from the person's Entra account and are not listed.
export function rolesText(roles: readonly PersonRole[]): string {
  const held = assignableRoles.filter((role) => roles.includes(role));
  return held.length === 0 ? "None" : held.map(roleLabel).join(", ");
}

export function lastSignInText(lastSignedInAt: Date | undefined): string {
  return lastSignedInAt === undefined ? "Never" : formatDateTimeIst(lastSignedInAt);
}
