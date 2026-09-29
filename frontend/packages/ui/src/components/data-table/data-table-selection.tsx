"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Checkbox } from "@dewiride/erp-ui/components/ui/checkbox";

import { formatCount, type DataTableNoun } from "./data-table-model";

export function DataTableSelectCheckbox({
  checked,
  label,
  disabled,
  onCheckedChange,
}: {
  checked: boolean | "indeterminate";
  label: string;
  disabled: boolean;
  onCheckedChange: (checked: boolean) => void;
}) {
  return (
    <Checkbox
      aria-label={label}
      checked={checked}
      disabled={disabled}
      onCheckedChange={(next) => onCheckedChange(next === true)}
    />
  );
}

export function DataTableSelectionSummary({
  count,
  noun,
  onClear,
}: {
  count: number;
  noun: DataTableNoun;
  onClear: () => void;
}) {
  return (
    <div className="flex min-h-7 flex-wrap items-center gap-x-3 gap-y-1 text-sm">
      <p role="status" data-testid="data-table-selection">
        {count > 0 ? `${formatCount(count)} ${count === 1 ? noun.one : noun.other} selected` : ""}
      </p>
      {count > 0 ? (
        <Button type="button" variant="ghost" size="sm" onClick={onClear}>
          Clear selection
        </Button>
      ) : null}
    </div>
  );
}
