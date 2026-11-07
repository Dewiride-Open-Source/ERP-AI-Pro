"use client";

import { Combobox, type ComboboxProps } from "@dewiride/erp-ui/components/combobox/combobox";
import { useMemo } from "react";

import { currencyOptions, listMessage } from "../../reference-lists/components/reference-options";
import { useReferenceList } from "../../reference-lists/hooks/use-reference-list";

export type CurrencyPickerProps = Omit<
  ComboboxProps,
  "options" | "emptyMessage" | "loading" | "loadingMessage"
> & {
  excluded?: readonly string[] | undefined;
};

export function CurrencyPicker({ value, excluded, ...comboboxProps }: CurrencyPickerProps) {
  const list = useReferenceList("currencies");
  const options = useMemo(() => currencyOptions(list, value, excluded), [list, value, excluded]);

  return (
    <Combobox
      {...comboboxProps}
      value={value}
      options={options}
      emptyMessage={listMessage(list, "currencies", "No currency matches.")}
    />
  );
}
