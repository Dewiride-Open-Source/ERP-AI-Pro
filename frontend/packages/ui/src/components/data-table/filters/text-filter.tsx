"use client";

import { SearchIcon } from "lucide-react";
import { useState, type ClipboardEvent } from "react";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { InputGroup, InputGroupAddon, InputGroupInput } from "@dewiride/erp-ui/components/ui/input-group";
import { cn } from "@dewiride/erp-ui/lib/utils";

import { trimmedPaste } from "./filter-values";

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

  // The list reads a filter without the space around it, so a value pasted with the space or line break it was copied with
  // enters without it, and the browser's length limit and pattern check judge the value the list will read.
  const paste = (event: ClipboardEvent<HTMLInputElement>) => {
    const input = event.currentTarget;
    const end = input.value.length;
    const pasted = trimmedPaste(
      input.value,
      { start: input.selectionStart ?? end, end: input.selectionEnd ?? end },
      event.clipboardData.getData("text/plain"),
      maxLength,
    );
    if (pasted === undefined) return;
    event.preventDefault();
    input.value = pasted.value;
    input.setSelectionRange(pasted.caret, pasted.caret);
    if (revealed) check(input);
  };

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
            onPaste={paste}
            autoComplete="off"
            spellCheck={false}
          />
        </InputGroup>
      )}
    </FormField>
  );
}
