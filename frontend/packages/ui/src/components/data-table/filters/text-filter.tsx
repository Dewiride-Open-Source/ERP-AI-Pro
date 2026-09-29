"use client";

import { SearchIcon } from "lucide-react";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { InputGroup, InputGroupAddon, InputGroupInput } from "@dewiride/erp-ui/components/ui/input-group";
import { cn } from "@dewiride/erp-ui/lib/utils";

export function DataTableTextFilter({
  name,
  label,
  defaultValue,
  placeholder,
  maxLength,
  className,
}: {
  name: string;
  label: string;
  defaultValue: string;
  placeholder?: string | undefined;
  maxLength: number;
  className?: string | undefined;
}) {
  return (
    <FormField label={label} className={cn("w-full sm:w-64", className)}>
      {(control) => (
        <InputGroup>
          <InputGroupAddon>
            <SearchIcon aria-hidden />
          </InputGroupAddon>
          <InputGroupInput
            {...control}
            type="search"
            name={name}
            defaultValue={defaultValue}
            placeholder={placeholder}
            maxLength={maxLength}
            autoComplete="off"
            spellCheck={false}
          />
        </InputGroup>
      )}
    </FormField>
  );
}
