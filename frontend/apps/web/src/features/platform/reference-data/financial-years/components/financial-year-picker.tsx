"use client";

import { Combobox, type ComboboxProps } from "@dewiride/erp-ui/components/combobox/combobox";
import { useMemo } from "react";

import { financialYearOptions, listMessage } from "../../reference-lists/components/reference-options";
import { useReferenceList } from "../../reference-lists/hooks/use-reference-list";

export type FinancialYearPickerProps = Omit<
  ComboboxProps,
  "options" | "emptyMessage" | "loading" | "loadingMessage"
>;

export function FinancialYearPicker({ value, ...comboboxProps }: FinancialYearPickerProps) {
  const list = useReferenceList("financialYears");
  const options = useMemo(() => financialYearOptions(list, value), [list, value]);

  return (
    <Combobox
      {...comboboxProps}
      value={value}
      options={options}
      emptyMessage={listMessage(list, "financial years", "No financial year matches.")}
    />
  );
}
