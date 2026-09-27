"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { DateRangeInput } from "@dewiride/erp-ui/components/pickers/date-range-input";
import { useController, type Control } from "react-hook-form";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";

import type { SupplierExample, SupplierExampleValues } from "../forms/supplier-example.schema";

import { leavesGroup } from "./field-focus";
import { rangeEndState } from "./range-end-state";

// react-hook-form re-validates a changed field by its own name and keeps only an error found at that name, so each end of
// the range is its own field; a new start date re-checks the end date because the order rule reports on the end.
export function ValidityField({
  control,
  fromId,
  toId,
}: {
  control: Control<SupplierExampleValues, unknown, SupplierExample>;
  fromId: string;
  toId: string;
}) {
  const { field: from, fieldState: fromState } = useController({
    control,
    name: "validity.from",
    rules: { deps: "validity.to" },
  });
  const { field: to, fieldState: toState } = useController({ control, name: "validity.to" });
  const fromErrors = fieldErrorMessages(fromState.error);
  const toErrors = fieldErrorMessages(toState.error);

  return (
    <FormField
      id={fromId}
      label="Validity"
      description="The first and the last day the agreement applies."
      required
      errors={[...(fromErrors ?? []), ...(toErrors ?? [])]}
    >
      {(frame) => (
        <div
          className="min-w-0"
          onBlur={(event) => {
            if (!leavesGroup(event)) return;
            from.onBlur();
            to.onBlur();
          }}
        >
          <DateRangeInput
            {...frame}
            toId={toId}
            ref={from.ref}
            toRef={to.ref}
            ends={{ from: rangeEndState(frame, fromErrors), to: rangeEndState(frame, toErrors) }}
            value={{ from: from.value, to: to.value }}
            onValueChange={(range) => {
              if (range.from !== from.value) from.onChange(range.from);
              if (range.to !== to.value) to.onChange(range.to);
            }}
          />
        </div>
      )}
    </FormField>
  );
}
