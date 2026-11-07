import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";

import type {
  ReferenceListColumn,
  ReferenceListRow,
} from "../../list-pages/components/reference-list-view.ts";
import type { FinancialYearItem } from "../../reference-lists/components/reference-options.ts";

export const quarterColumns: readonly ReferenceListColumn[] = [
  { id: "quarter", header: "Quarter", title: true },
  { id: "from", header: "From" },
  { id: "until", header: "Until" },
];

export const periodColumns: readonly ReferenceListColumn[] = [
  { id: "month", header: "Month", title: true },
  { id: "period", header: "Period" },
  { id: "quarter", header: "Quarter" },
  { id: "from", header: "From" },
  { id: "until", header: "Until" },
];

export function yearDays(year: Pick<FinancialYearItem, "start" | "end">): string {
  return `From ${formatDisplayDate(year.start)} to ${formatDisplayDate(year.end)}.`;
}

export function quarterRows(year: FinancialYearItem): readonly ReferenceListRow[] {
  return year.quarters.map((quarter) => ({
    id: quarter.start,
    cells: {
      quarter: quarter.label,
      from: formatDisplayDate(quarter.start),
      until: formatDisplayDate(quarter.end),
    },
  }));
}

export function periodRows(year: FinancialYearItem): readonly ReferenceListRow[] {
  return year.periods.map((period) => ({
    id: period.start,
    cells: {
      month: period.label,
      period: String(period.number),
      quarter:
        year.quarters.find((quarter) => quarter.number === period.quarter)?.label ?? String(period.quarter),
      from: formatDisplayDate(period.start),
      until: formatDisplayDate(period.end),
    },
  }));
}
