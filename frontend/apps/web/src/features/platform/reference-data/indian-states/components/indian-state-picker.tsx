"use client";

import { Combobox, type ComboboxProps } from "@dewiride/erp-ui/components/combobox/combobox";
import { useMemo } from "react";

import {
  indianStateOptions,
  listMessage,
  matchReferenceOptions,
} from "../../reference-lists/components/reference-options";
import { useReferenceList, useReferenceToday } from "../../reference-lists/hooks/use-reference-list";

export type IndianStatePickerProps = Omit<
  ComboboxProps,
  "options" | "emptyMessage" | "loading" | "loadingMessage" | "filter"
> & {
  on?: string | null | undefined;
};

// A GST State code stands for one territory on a date, so a document's own date picks the codes offered; without one the
// codes in force today in India are.
export function IndianStatePicker({ value, on = null, ...comboboxProps }: IndianStatePickerProps) {
  const list = useReferenceList("indianStates");
  const today = useReferenceToday();
  const options = useMemo(() => indianStateOptions(list, on, today, value), [list, on, today, value]);

  return (
    <Combobox
      {...comboboxProps}
      value={value}
      options={options}
      filter={matchReferenceOptions}
      emptyMessage={listMessage(
        list,
        "Indian states and union territories",
        "No state or union territory matches.",
      )}
    />
  );
}
