import { defineList } from "@/shared/lists/list-definition";

export const attachmentsList = defineList({
  pageSizes: [10, 20, 50, 100],
  defaultPageSize: 20,
  sortFields: ["fileName", "contentType", "sizeBytes", "createdAt"],
  defaultSort: { field: "createdAt", direction: "desc" },
  filters: [
    { kind: "text", param: "name", field: "fileName", operator: "contains", maxLength: 255 },
    { kind: "options", param: "type", field: "contentType" },
    { kind: "dateRange", param: "uploaded", field: "createdAt", instant: true },
  ],
  columns: ["fileName", "contentType", "sizeBytes", "createdAt", "scanStatus"],
  hideableColumns: ["contentType", "sizeBytes", "createdAt", "scanStatus"],
});
