"use client";

import { Combobox, type ComboboxProps } from "@dewiride/erp-ui/components/combobox/combobox";
import { useMemo } from "react";

import { financialPeriodOptions, listMessage } from "../../reference-lists/components/reference-options";
import { useReferenceList } from "../../reference-lists/hooks/use-reference-list";

export type FinancialPeriodPickerProps = Omit<
  ComboboxProps,
  "options" | "emptyMessage" | "loading" | "loadingMessage"
> & {
  year: string | null;
};

// A period's value is its first day, which names it in every financial year; the year chosen beside it decides which
// twelve periods are offered.
export function FinancialPeriodPicker({ value, year, ...comboboxProps }: FinancialPeriodPickerProps) {
  const list = useReferenceList("financialYears");
  const options = useMemo(() => financialPeriodOptions(list, year, value), [list, year, value]);

  return (
    <Combobox
      {...comboboxProps}
      value={value}
      options={options}
      emptyMessage={
        year === null
          ? "Choose a financial year first."
          : listMessage(list, "financial years", "No period matches.")
      }
    />
  );
}
