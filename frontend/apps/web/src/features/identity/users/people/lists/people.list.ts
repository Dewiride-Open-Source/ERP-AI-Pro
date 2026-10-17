import type { PersonStatus } from "@dewiride/erp-api-client";

import { defineList } from "@/shared/lists/list-definition";
import { isDateRange, withFilters, type ListFilterValue, type ListQuery } from "@/shared/lists/list-query";

export const personStatuses: readonly PersonStatus[] = ["active", "deactivated"];

export const displayNameMaxLength = 256;

export const workEmailMaxLength = 254;

export const employeeCodeMaxLength = 32;

export const peopleList = defineList({
  pageSizes: [10, 20, 50, 100],
  defaultPageSize: 10,
  sortFields: [
    "displayName",
    "workEmail",
    "employeeCode",
    "department",
    "designation",
    "manager",
    "dateOfJoining",
    "status",
    "lastSignedInAt",
  ],
  defaultSort: { field: "displayName", direction: "asc" },
  filters: [
    {
      kind: "text",
      param: "name",
      field: "displayName",
      operator: "contains",
      maxLength: displayNameMaxLength,
    },
    { kind: "text", param: "email", field: "workEmail", operator: "contains", maxLength: workEmailMaxLength },
    {
      kind: "text",
      param: "code",
      field: "employeeCode",
      operator: "contains",
      maxLength: employeeCodeMaxLength,
    },
    { kind: "options", param: "status", field: "status", values: personStatuses },
    { kind: "options", param: "department", field: "departmentId" },
    { kind: "options", param: "designation", field: "designationId" },
    { kind: "dateRange", param: "joined", field: "dateOfJoining", instant: false },
  ],
  columns: [
    "displayName",
    "workEmail",
    "employeeCode",
    "department",
    "designation",
    "manager",
    "dateOfJoining",
    "status",
    "roles",
    "lastSignedInAt",
  ],
  hideableColumns: [
    "workEmail",
    "employeeCode",
    "department",
    "designation",
    "manager",
    "dateOfJoining",
    "status",
    "roles",
    "lastSignedInAt",
  ],
});

const idFilterParameters: ReadonlySet<string> = new Set(["department", "designation"]);

const wellFormedId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// The API refuses a department or designation filter whose value is not an id, which would fail the whole list, so an address
// that names one keeps only its ids, written as the API writes them.
export function withWellFormedIds(query: ListQuery): ListQuery {
  let changed = false;
  const filters: Record<string, ListFilterValue> = {};
  for (const [parameter, value] of Object.entries(query.filters)) {
    if (!idFilterParameters.has(parameter) || typeof value === "string" || isDateRange(value)) {
      filters[parameter] = value;
      continue;
    }
    const ids = [
      ...new Set(value.filter((id) => wellFormedId.test(id)).map((id) => id.toLowerCase())),
    ].sort();
    if (ids.length !== value.length || ids.some((id, index) => id !== value[index])) changed = true;
    if (ids.length > 0) filters[parameter] = ids;
  }
  return changed ? withFilters(query, filters) : query;
}
