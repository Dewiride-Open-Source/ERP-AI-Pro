import type { ParameterColumn, RowChange } from "../../parameter-sets/components/set-items.ts";

import { cellValue, rowCellText, sourceHeader } from "./cell-text.ts";

export interface ChangedCell {
  readonly label: string;
  readonly value: string;
  readonly was: string;
}

// An entry a version changed shows only what differs from the entry it replaced, each value beside the one before it, as the
// audit trail shows a change; the clause it is copied from counts too, because a change of law cites a new one.
export function changedCells(columns: readonly ParameterColumn[], change: RowChange): readonly ChangedCell[] {
  const cells = columns.flatMap((column) => {
    const before = cellValue(change.before, column.name);
    const after = cellValue(change.after, column.name);
    return before === after
      ? []
      : [
          {
            label: column.title,
            value: rowCellText(column, change.after),
            was: rowCellText(column, change.before),
          },
        ];
  });
  return change.before.source === change.after.source
    ? cells
    : [...cells, { label: sourceHeader, value: change.after.source, was: change.before.source }];
}
