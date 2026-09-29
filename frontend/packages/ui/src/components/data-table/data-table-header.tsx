"use client";

import { ArrowDownIcon, ArrowUpIcon, ChevronsUpDownIcon } from "lucide-react";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { cn } from "@dewiride/erp-ui/lib/utils";

import type { DataTableSortDirection } from "./data-table-model";

export function DataTableColumnHeader({
  header,
  sorted,
  align,
  disabled,
  onSort,
}: {
  header: string;
  sorted: DataTableSortDirection | false;
  align: "start" | "end";
  disabled: boolean;
  onSort: () => void;
}) {
  const Icon = sorted === "asc" ? ArrowUpIcon : sorted === "desc" ? ArrowDownIcon : ChevronsUpDownIcon;

  return (
    <Button
      type="button"
      variant="ghost"
      size="sm"
      disabled={disabled}
      className={cn("h-8", align === "end" ? "-me-2.5 flex-row-reverse" : "-ms-2.5")}
      onClick={onSort}
    >
      {header}
      <Icon aria-hidden className={cn(sorted === false && "text-muted-foreground")} />
    </Button>
  );
}
