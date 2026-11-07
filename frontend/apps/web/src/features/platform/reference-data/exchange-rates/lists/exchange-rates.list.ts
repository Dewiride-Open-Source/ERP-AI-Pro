import { defineList } from "@/shared/lists/list-definition";

// Ten rates a page by default, because a phone shows each rate as a card and a full-page capture of twenty cards would pass
// the 16,384 device pixels a browser paints.
export const exchangeRatesList = defineList({
  pageSizes: [10, 20, 50, 100],
  defaultPageSize: 10,
  sortFields: ["rateDate", "currency"],
  defaultSort: { field: "rateDate", direction: "desc" },
  filters: [
    { kind: "options", param: "currency", field: "currency" },
    { kind: "dateRange", param: "day", field: "rateDate", instant: false },
  ],
  columns: ["rateDate", "currency", "rate", "unit", "source"],
  hideableColumns: ["source"],
});
