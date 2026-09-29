"use client";

import { ChevronDownIcon } from "lucide-react";
import { useState } from "react";

import { FormField, formFieldLabelId } from "@dewiride/erp-ui/components/forms/form-field";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@dewiride/erp-ui/components/ui/dropdown-menu";
import { cn } from "@dewiride/erp-ui/lib/utils";

import { useHydrated } from "../use-hydrated";

import { optionsSummary, toggleOption, type DataTableFilterOption } from "./filter-values";

export type { DataTableFilterOption } from "./filter-values";

export function DataTableOptionsFilter({
  name,
  label,
  options,
  defaultValues,
  className,
}: {
  name: string;
  label: string;
  options: readonly DataTableFilterOption[];
  defaultValues: readonly string[];
  className?: string | undefined;
}) {
  const [selected, setSelected] = useState<readonly string[]>(defaultValues);
  const interactive = useHydrated();

  return (
    <FormField label={label} className={cn("w-full sm:w-48", className)}>
      {(control) => (
        <>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                id={control.id}
                type="button"
                variant="outline"
                disabled={!interactive}
                aria-labelledby={`${formFieldLabelId(control.id)} ${control.id}`}
                aria-describedby={control["aria-describedby"]}
                className="w-full justify-between font-normal"
              >
                <span className="truncate">{optionsSummary(selected, options)}</span>
                <ChevronDownIcon aria-hidden className="text-muted-foreground" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent
              align="start"
              className="w-56 max-w-(--radix-dropdown-menu-content-available-width)"
            >
              <DropdownMenuGroup>
                {options.map((option) => (
                  <DropdownMenuCheckboxItem
                    key={option.value}
                    checked={selected.includes(option.value)}
                    onCheckedChange={(checked) =>
                      setSelected((current) => toggleOption(current, option.value, checked, options))
                    }
                    onSelect={(event) => event.preventDefault()}
                  >
                    {option.label}
                  </DropdownMenuCheckboxItem>
                ))}
              </DropdownMenuGroup>
              {selected.length > 0 ? (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onSelect={() => setSelected([])}>Clear choices</DropdownMenuItem>
                </>
              ) : null}
            </DropdownMenuContent>
          </DropdownMenu>
          {selected.map((value) => (
            <input key={value} type="hidden" name={name} value={value} />
          ))}
        </>
      )}
    </FormField>
  );
}
