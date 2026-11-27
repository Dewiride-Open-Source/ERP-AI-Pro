import type { ApprovalState } from "@dewiride/erp-api-client";

import { defineList, type ListDefinition } from "@/shared/lists/list-definition";

export type RequestBox = "waiting" | "mine" | "decided" | "all";

export const approvalStates: readonly ApprovalState[] = [
  "submitted",
  "approved",
  "returned",
  "rejected",
  "withdrawn",
];

// Every request in the waiting box still waits, so that box is neither filtered by state nor sorted by when a request closed.
export const waitingRequestsList = defineList({
  pageSizes: [10, 20, 50, 100],
  defaultPageSize: 10,
  sortFields: ["dueAt", "submittedAt"],
  defaultSort: { field: "dueAt", direction: "asc" },
  filters: [{ kind: "options", param: "kind", field: "kind" }],
  columns: ["request", "requestedFor", "submittedAt", "step", "dueAt"],
  hideableColumns: ["requestedFor", "submittedAt", "step"],
});

export const mineRequestsList = defineList({
  pageSizes: [10, 20, 50, 100],
  defaultPageSize: 10,
  sortFields: ["submittedAt", "dueAt", "closedAt"],
  defaultSort: { field: "submittedAt", direction: "desc" },
  filters: [
    { kind: "options", param: "kind", field: "kind" },
    { kind: "options", param: "state", field: "state", values: approvalStates },
  ],
  columns: ["request", "requestedFor", "submittedAt", "state", "dueAt", "closedAt"],
  hideableColumns: ["requestedFor", "submittedAt", "dueAt", "closedAt"],
});

export const decidedRequestsList = defineList({
  pageSizes: [10, 20, 50, 100],
  defaultPageSize: 10,
  sortFields: ["submittedAt", "dueAt", "closedAt"],
  defaultSort: { field: "submittedAt", direction: "desc" },
  filters: [
    { kind: "options", param: "kind", field: "kind" },
    { kind: "options", param: "state", field: "state", values: approvalStates },
  ],
  columns: ["request", "requestedFor", "submittedAt", "state", "dueAt", "closedAt"],
  hideableColumns: ["requestedFor", "submittedAt", "dueAt", "closedAt"],
});

export const allRequestsList = defineList({
  pageSizes: [10, 20, 50, 100],
  defaultPageSize: 10,
  sortFields: ["submittedAt", "dueAt", "closedAt"],
  defaultSort: { field: "submittedAt", direction: "desc" },
  filters: [
    { kind: "options", param: "kind", field: "kind" },
    { kind: "options", param: "state", field: "state", values: approvalStates },
  ],
  columns: ["request", "requestedFor", "submittedAt", "state", "dueAt", "closedAt"],
  hideableColumns: ["requestedFor", "submittedAt", "dueAt", "closedAt"],
});

export const requestLists: Readonly<Record<RequestBox, ListDefinition>> = {
  waiting: waitingRequestsList,
  mine: mineRequestsList,
  decided: decidedRequestsList,
  all: allRequestsList,
};
