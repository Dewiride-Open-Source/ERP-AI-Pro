import type { IndianStateKind } from "@dewiride/erp-api-client";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";

import type {
  ReferenceListColumn,
  ReferenceListRow,
} from "../../list-pages/components/reference-list-view.ts";
import type { IndianStateItem } from "../../reference-lists/components/reference-options.ts";

export const inForce = "In force";

const kindLabels: Readonly<Record<IndianStateKind, string>> = {
  state: "State",
  unionTerritory: "Union territory",
  otherTerritory: "Other territory",
};

export const indianStateColumns: readonly ReferenceListColumn[] = [
  { id: "name", header: "Name", title: true },
  { id: "code", header: "GST State code" },
  { id: "kind", header: "Type" },
  { id: "from", header: "From" },
  { id: "until", header: "Until" },
];

export function kindLabel(kind: IndianStateKind): string {
  return kindLabels[kind];
}

// A code stands for one territory from one day until another, and a code reused for a new territory is a row of its own,
// so a row is named by its code and its first day.
export function indianStateRow(state: IndianStateItem): ReferenceListRow {
  return {
    id: `${state.gstCode}-${state.validFrom}`,
    cells: {
      code: state.gstCode,
      name: state.name,
      kind: kindLabel(state.kind),
      from: formatDisplayDate(state.validFrom),
      until: state.validUntil === null ? inForce : formatDisplayDate(state.validUntil),
    },
  };
}
