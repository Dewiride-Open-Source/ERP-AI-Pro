"use client";

import { SearchIcon } from "lucide-react";
import { useState } from "react";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { InputGroup, InputGroupAddon, InputGroupInput } from "@dewiride/erp-ui/components/ui/input-group";
import { cn } from "@dewiride/erp-ui/lib/utils";

export interface DataTableTextFilterPattern {
  readonly source: string;
  readonly message: string;
}

export function DataTableTextFilter({
  name,
  label,
  defaultValue,
  placeholder,
  maxLength,
  pattern,
  className,
}: {
  name: string;
  label: string;
  defaultValue: string;
  placeholder?: string | undefined;
  maxLength: number;
  pattern?: DataTableTextFilterPattern | undefined;
  className?: string | undefined;
}) {
  // A pattern's message appears when the form is sent and then follows every change, never on blur, so it cannot move
  // "Apply filters" from under a pointer mid-click.
  const [revealed, setRevealed] = useState(false);
  const [problem, setProblem] = useState<string | undefined>(undefined);
  const check = (input: HTMLInputElement) =>
    setProblem(pattern !== undefined && input.validity.patternMismatch ? pattern.message : undefined);

  return (
    <FormField
      label={label}
      errors={problem === undefined ? undefined : [problem]}
      className={cn("w-full sm:w-64", className)}
    >
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
            pattern={pattern?.source}
            onInvalid={
              pattern === undefined
                ? undefined
                : (event) => {
                    setRevealed(true);
                    check(event.currentTarget);
                  }
            }
            onChange={revealed ? (event) => check(event.currentTarget) : undefined}
            autoComplete="off"
            spellCheck={false}
          />
        </InputGroup>
      )}
    </FormField>
  );
}
