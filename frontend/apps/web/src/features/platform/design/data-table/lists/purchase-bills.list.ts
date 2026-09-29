import { defineList } from "@/shared/lists/list-definition";

export const purchaseBillStatuses = ["draft", "approved", "paid", "overdue"] as const;

export type PurchaseBillStatus = (typeof purchaseBillStatuses)[number];

export const purchaseBillsList = defineList({
  pageSizes: [10, 20, 50],
  defaultPageSize: 10,
  sortFields: ["number", "supplier", "billDate", "dueDate", "amount"],
  defaultSort: { field: "dueDate", direction: "asc" },
  filters: [
    { kind: "text", param: "supplier", field: "supplier", operator: "contains", maxLength: 100 },
    { kind: "options", param: "status", field: "status", values: purchaseBillStatuses },
    { kind: "dateRange", param: "due", field: "dueDate", instant: false },
  ],
  columns: ["number", "supplier", "state", "billDate", "dueDate", "amount", "status"],
  hideableColumns: ["supplier", "state", "billDate", "dueDate", "amount", "status"],
});
