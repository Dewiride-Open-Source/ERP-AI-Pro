import type { JobRunStatus } from "@dewiride/erp-api-client";

import { defineList } from "@/shared/lists/list-definition";

export const runStatuses: readonly JobRunStatus[] = [
  "pending",
  "running",
  "failed",
  "succeeded",
  "deadLettered",
  "cancelled",
];

export const runKeyMaxLength = 200;

// A run's key holds only these characters, so a key filter with any other could match no run; the filter form refuses it in
// the browser instead of showing an empty list.
export const runKeyPattern = "[A-Za-z0-9._:+\\-]+";

export const runsList = defineList({
  pageSizes: [10, 20, 50, 100],
  defaultPageSize: 10,
  sortFields: ["createdAt", "dueAt", "finishedAt"],
  defaultSort: { field: "createdAt", direction: "desc" },
  filters: [
    { kind: "options", param: "job", field: "jobName" },
    { kind: "options", param: "status", field: "status", values: runStatuses },
    {
      kind: "text",
      param: "key",
      field: "idempotencyKey",
      operator: "contains",
      maxLength: runKeyMaxLength,
      pattern: runKeyPattern,
    },
    { kind: "dateRange", param: "created", field: "createdAt", instant: true },
  ],
  columns: ["job", "status", "idempotencyKey", "createdAt", "dueAt", "finishedAt", "tries", "lastError"],
  hideableColumns: ["idempotencyKey", "createdAt", "dueAt", "finishedAt", "tries", "lastError"],
});
