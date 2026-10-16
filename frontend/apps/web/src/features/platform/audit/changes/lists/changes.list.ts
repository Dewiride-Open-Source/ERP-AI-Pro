import type { ChangeOperation } from "@dewiride/erp-api-client";

import { defineList } from "@/shared/lists/list-definition";

// The change keys the recorded entity types declare (ADR-0043). A key never changes once records carry it, so an address that
// filters by one keeps working.
export const changeKeys = [
  "identity.users.people",
  "identity.users.departments",
  "identity.users.designations",
  "identity.users.positions",
  "platform.attachments.files",
] as const;

export const changeOperations: readonly ChangeOperation[] = ["created", "updated", "deleted", "restored"];

// The API refuses an actor or record filter that is not a UUID, so the filter form refuses it first, in the browser.
export const uuidPattern = "[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}";

export const uuidLength = 36;

export const changesList = defineList({
  pageSizes: [20, 50, 100],
  defaultPageSize: 20,
  sortFields: ["occurredAt"],
  defaultSort: { field: "occurredAt", direction: "desc" },
  filters: [
    { kind: "options", param: "type", field: "entityType", values: changeKeys },
    { kind: "options", param: "action", field: "operation", values: changeOperations },
    { kind: "dateRange", param: "changed", field: "occurredAt", instant: true },
    {
      kind: "text",
      param: "actor",
      field: "actorId",
      operator: "eq",
      maxLength: uuidLength,
      pattern: uuidPattern,
    },
    {
      kind: "text",
      param: "record",
      field: "subjectId",
      operator: "eq",
      maxLength: uuidLength,
      pattern: uuidPattern,
    },
  ],
  columns: [],
  hideableColumns: [],
});
