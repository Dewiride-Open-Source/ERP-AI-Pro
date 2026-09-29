"use client";

import { Columns3Icon } from "lucide-react";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from "@dewiride/erp-ui/components/ui/dropdown-menu";

export interface DataTableColumnChoice {
  readonly id: string;
  readonly header: string;
  readonly visible: boolean;
  readonly toggleable: boolean;
}

export function DataTableColumnsMenu({
  columns,
  disabled,
  onVisibleChange,
}: {
  columns: readonly DataTableColumnChoice[];
  disabled: boolean;
  onVisibleChange: (columnId: string, visible: boolean) => void;
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={disabled}
          data-testid="data-table-columns"
        >
          <Columns3Icon aria-hidden />
          Columns
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56 max-w-(--radix-dropdown-menu-content-available-width)">
        <DropdownMenuGroup>
          <DropdownMenuLabel>Show columns</DropdownMenuLabel>
          {columns.map((column) => (
            <DropdownMenuCheckboxItem
              key={column.id}
              checked={column.visible}
              disabled={!column.toggleable}
              onCheckedChange={(checked) => onVisibleChange(column.id, checked)}
              onSelect={(event) => event.preventDefault()}
            >
              {column.header}
            </DropdownMenuCheckboxItem>
          ))}
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
