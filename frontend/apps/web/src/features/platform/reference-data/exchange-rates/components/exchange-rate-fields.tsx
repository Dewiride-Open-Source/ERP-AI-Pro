"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { AmountInput } from "@dewiride/erp-ui/components/inputs/amount-input";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@dewiride/erp-ui/components/ui/select";
import type { FieldError, RefCallBack, UseFormRegisterReturn } from "react-hook-form";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";

import { exchangeRateUnits, rateScale, sourceMaxLength } from "../forms/exchange-rate.schema";

import { unitText } from "./exchange-rate-display";

export interface ControlledText {
  readonly name: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly onBlur: () => void;
  readonly ref: RefCallBack;
}

export function RateField({
  id,
  field,
  error,
}: {
  id: string;
  field: ControlledText;
  error: FieldError | undefined;
}) {
  return (
    <FormField
      id={id}
      label="Rate"
      description={`Rupees for the units of the currency, with up to ${rateScale} digits after the decimal point.`}
      required
      errors={fieldErrorMessages(error)}
    >
      {(frame) => (
        <AmountInput
          {...frame}
          ref={field.ref}
          name={field.name}
          value={field.value}
          onValueChange={field.onChange}
          onBlur={field.onBlur}
          scale={rateScale}
          placeholder="0.0000"
        />
      )}
    </FormField>
  );
}

export function UnitField({
  id,
  field,
  error,
}: {
  id: string;
  field: ControlledText;
  error: FieldError | undefined;
}) {
  return (
    <FormField
      id={id}
      label="Units"
      description="How many units of the currency the rate is for, such as 100 for the yen."
      required
      errors={fieldErrorMessages(error)}
    >
      {(frame) => (
        <Select
          value={field.value}
          onValueChange={field.onChange}
          onOpenChange={(open) => {
            if (!open) field.onBlur();
          }}
        >
          <SelectTrigger {...frame} ref={field.ref} className="w-full">
            <SelectValue placeholder="Choose the units" />
          </SelectTrigger>
          <SelectContent>
            {exchangeRateUnits.map((unit) => (
              <SelectItem key={unit} value={String(unit)}>
                {unitText(unit)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
    </FormField>
  );
}

export function SourceField({
  id,
  registration,
  error,
}: {
  id: string;
  registration: UseFormRegisterReturn<"source">;
  error: FieldError | undefined;
}) {
  return (
    <FormField
      id={id}
      label="Source"
      description={`Optional. Where the rate comes from, such as FBIL reference rate; up to ${sourceMaxLength} characters.`}
      errors={fieldErrorMessages(error)}
    >
      {(frame) => <Input {...frame} {...registration} autoComplete="off" />}
    </FormField>
  );
}
