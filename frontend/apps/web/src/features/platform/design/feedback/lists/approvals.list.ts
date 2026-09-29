import { defineList } from "@/shared/lists/list-definition";

export const approvalsList = defineList({
  pageSizes: [10],
  defaultPageSize: 10,
  sortFields: ["raisedOn", "requestedBy"],
  defaultSort: { field: "raisedOn", direction: "asc" },
  filters: [],
  columns: ["request", "requestedBy", "raisedOn"],
  hideableColumns: ["requestedBy", "raisedOn"],
});
