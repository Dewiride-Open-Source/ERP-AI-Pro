"use client";

import { useId } from "react";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@dewiride/erp-ui/components/ui/select";
import { cn } from "@dewiride/erp-ui/lib/utils";

import {
  sortDirectionLabel,
  type DataTableSort,
  type DataTableSortDirection,
  type DataTableSortKind,
} from "./data-table-model";

export interface DataTableSortChoice {
  readonly columnId: string;
  readonly header: string;
  readonly kind: DataTableSortKind;
}

const directions: readonly DataTableSortDirection[] = ["asc", "desc"];

function choiceValue(columnId: string, direction: DataTableSortDirection): string {
  return `${columnId}:${direction}`;
}

function choiceLabel(choice: DataTableSortChoice, direction: DataTableSortDirection): string {
  return `${choice.header}, ${sortDirectionLabel(choice.kind, direction)}`;
}

export function DataTableSortSelect({
  choices,
  sort,
  onSortChange,
  disabled,
  className,
}: {
  choices: readonly DataTableSortChoice[];
  sort: DataTableSort;
  onSortChange: (sort: DataTableSort) => void;
  disabled: boolean;
  className?: string | undefined;
}) {
  const labelId = useId();
  const sorted = choices.find((choice) => choice.columnId === sort.columnId);

  return (
    <div className={cn("flex items-center gap-2 text-sm", className)}>
      <span id={labelId} className="text-muted-foreground">
        Sort by
      </span>
      <Select
        value={choiceValue(sort.columnId, sort.direction)}
        disabled={disabled}
        onValueChange={(value) => {
          const separator = value.lastIndexOf(":");
          onSortChange({
            columnId: value.slice(0, separator),
            direction: value.slice(separator + 1) === "desc" ? "desc" : "asc",
          });
        }}
      >
        <SelectTrigger size="sm" aria-labelledby={labelId} data-testid="data-table-sort">
          <SelectValue>{sorted === undefined ? null : choiceLabel(sorted, sort.direction)}</SelectValue>
        </SelectTrigger>
        <SelectContent>
          {choices.flatMap((choice) =>
            directions.map((direction) => (
              <SelectItem
                key={choiceValue(choice.columnId, direction)}
                value={choiceValue(choice.columnId, direction)}
              >
                {choiceLabel(choice, direction)}
              </SelectItem>
            )),
          )}
        </SelectContent>
      </Select>
    </div>
  );
}
