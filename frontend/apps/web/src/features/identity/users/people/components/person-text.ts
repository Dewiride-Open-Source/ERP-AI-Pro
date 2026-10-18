import type { ErpRole, PersonDeactivationReason, PersonRole, PersonStatus } from "@dewiride/erp-api-client";

import { formatDateTimeIst } from "@/shared/format/dates";

import { assignableRoles } from "../../registration/forms/register-person.schema.ts";

const statusLabels: Readonly<Record<PersonStatus, string>> = {
  active: "Active",
  deactivated: "Deactivated",
};

const deactivationNotes: Readonly<Record<PersonDeactivationReason, string>> = {
  administrator: "Deactivated by an administrator.",
  accountDisabled: "Deactivated by the account check, which found their Microsoft account disabled.",
  accountDeleted: "Deactivated by the account check, which found their Microsoft account deleted.",
};

export const needsNewManager = "Needs a new manager";

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

export const departmentNameUnavailable = "Department (name not available)";

export const designationNameUnavailable = "Designation (name not available)";

export const departmentsUnavailable =
  "Departments could not be read, so the list cannot be filtered by department now.";

export const designationsUnavailable =
  "Designations could not be read, so the list cannot be filtered by designation now.";

export function statusLabel(status: PersonStatus | undefined): string {
  return status === undefined ? notRecorded : statusLabels[status];
}

export function deactivationNote(reason: PersonDeactivationReason | undefined): string | undefined {
  return reason === undefined ? undefined : deactivationNotes[reason];
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
  return lastSignedInAt === undefined ? "Not signed in yet" : formatDateTimeIst(lastSignedInAt);
}
