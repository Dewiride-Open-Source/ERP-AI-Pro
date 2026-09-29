"use client";

import { useState } from "react";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateRangeInput } from "@dewiride/erp-ui/components/pickers/date-range-input";
import { cn } from "@dewiride/erp-ui/lib/utils";

import { dateRangeProblems, submittedDate, type DateRangeFilterValue } from "./filter-values";

export type { DateRangeFilterValue } from "./filter-values";

function validityRef(message: string | undefined, onInvalid: () => void) {
  return (input: HTMLInputElement | null) => {
    if (input === null) return undefined;
    input.setCustomValidity(message ?? "");
    input.addEventListener("invalid", onInvalid);
    return () => input.removeEventListener("invalid", onInvalid);
  };
}

export function DataTableDateRangeFilter({
  name,
  label,
  defaultValue,
  className,
}: {
  name: string;
  label: string;
  defaultValue: DateRangeFilterValue;
  className?: string | undefined;
}) {
  const [value, setValue] = useState<DateRangeFilterValue>(defaultValue);
  const [revealed, setRevealed] = useState(false);
  const problems = dateRangeProblems(value);
  const reveal = () => setRevealed(true);
  const shown = revealed ? problems : { from: undefined, to: undefined };
  const errors = [shown.from, shown.to].filter((message) => message !== undefined);

  return (
    <FormField label={label} errors={errors} className={cn("w-full sm:w-80", className)}>
      {(control) => (
        <>
          <DateRangeInput
            {...control}
            ref={validityRef(problems.from, reveal)}
            toRef={validityRef(problems.to, reveal)}
            value={value}
            onValueChange={setValue}
            onBlur={reveal}
            ends={{
              from: {
                invalid: shown.from !== undefined,
                describedBy: shown.from === undefined ? undefined : control["aria-describedby"],
              },
              to: {
                invalid: shown.to !== undefined,
                describedBy: shown.to === undefined ? undefined : control["aria-describedby"],
              },
            }}
          />
          <input type="hidden" name={`${name}From`} value={submittedDate(value.from)} />
          <input type="hidden" name={`${name}To`} value={submittedDate(value.to)} />
        </>
      )}
    </FormField>
  );
}
