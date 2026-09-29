"use client";

import { useState } from "react";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateRangeInput } from "@dewiride/erp-ui/components/pickers/date-range-input";
import { cn } from "@dewiride/erp-ui/lib/utils";

import { useHydrated } from "../use-hydrated";

import {
  dateRangeProblems,
  submittedDate,
  type DateRangeFilterValue,
  type DateRangeProblems,
} from "./filter-values";

export type { DateRangeFilterValue } from "./filter-values";

type RangeEnd = keyof DateRangeProblems;

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
  const interactive = useHydrated();
  const [value, setValue] = useState<DateRangeFilterValue>(defaultValue);
  const [revealed, setRevealed] = useState<Readonly<Record<RangeEnd, boolean>>>({ from: false, to: false });
  const problems = dateRangeProblems(value);
  const reveal = (end: RangeEnd) => () => setRevealed((current) => ({ ...current, [end]: true }));
  const shown: DateRangeProblems = {
    from: revealed.from ? problems.from : undefined,
    to: revealed.to ? problems.to : undefined,
  };
  const errors = [shown.from, shown.to].filter((message) => message !== undefined);

  return (
    <FormField label={label} errors={errors} className={cn("w-full sm:w-80", className)}>
      {(control) => (
        <>
          <DateRangeInput
            {...control}
            ref={validityRef(problems.from, reveal("from"))}
            toRef={validityRef(problems.to, reveal("to"))}
            value={value}
            onValueChange={setValue}
            disabled={!interactive}
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
