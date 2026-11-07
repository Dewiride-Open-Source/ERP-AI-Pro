"use client";

import { Combobox, type ComboboxProps } from "@dewiride/erp-ui/components/combobox/combobox";
import { useMemo } from "react";

import { countryOptions, listMessage } from "../../reference-lists/components/reference-options";
import { useReferenceList } from "../../reference-lists/hooks/use-reference-list";

export type CountryPickerProps = Omit<
  ComboboxProps,
  "options" | "emptyMessage" | "loading" | "loadingMessage"
>;

export function CountryPicker({ value, ...comboboxProps }: CountryPickerProps) {
  const list = useReferenceList("countries");
  const options = useMemo(() => countryOptions(list, value), [list, value]);

  return (
    <Combobox
      {...comboboxProps}
      value={value}
      options={options}
      emptyMessage={listMessage(list, "countries", "No country matches.")}
    />
  );
}
